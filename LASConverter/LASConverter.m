classdef LASConverter
    methods(Static)
        
        function LASPoints2Bytes(points, lasName, pointsPerFile)
            
            x = points(:,1);
            y = points(:,2);
            z = points(:,3);
            c = points(:,5);
            xyzClass = [x - min(x), y - min(y), z - min(z), double(c)];
            
            LASConverter.LASData2Bytes_Octree(xyzClass, lasName, pointsPerFile);
        end
    
        function [voxelLinearIndex, voxelIDs] = computeOptimalGrid(x, y, z, maxNPointsPerMesh)
            voxelSize = (max(x) - min(x)) / 5;
            
            minX = min(x);
            minY = min(y);
            minZ = min(z);
            maxNPoints = maxNPointsPerMesh+1;
            
            while (maxNPoints > maxNPointsPerMesh)
                
                voxelSize = voxelSize * 0.9;

                voxelIndexX = floor((x - minX) / voxelSize)+1;
                voxelIndexY = floor((y - minY) / voxelSize)+1;
                voxelIndexZ = floor((z - minZ) / voxelSize)+1;
                
                gridSize = [max(voxelIndexX),max(voxelIndexY),max(voxelIndexZ)];
                voxelLinearIndex = sub2ind(gridSize, voxelIndexX, voxelIndexY, voxelIndexZ);
                
                voxelIDs = unique(voxelLinearIndex);
                [a,~]=hist(voxelLinearIndex,unique(voxelLinearIndex));
                maxNPoints = max(a);
            end
        end
        
        function LASFile2Bytes(filename, lasName, pointsPerFile)
            las = lasdata(filename);
            class = las.get_classification();
            xyzClass = [las.x - min(las.x), las.y - min(las.y), ...
                        las.z - min(las.z), double(class)];
                    
            LASConverter.LASData2Bytes_Voxels(xyzClass, lasName, pointsPerFile);
        end
        
        function LASData2Bytes_Sequential(xyzClass, lasName, pointsPerFile)
            LASConverter.resetFolder(lasName);
                
            pointer = 1;
            while pointer < length(xyzClass)
                maxP = pointer + pointsPerFile;
                if maxP > length(xyzClass)
                    maxP = length(xyzClass);
                end
                outFN = sprintf("%s/Points_%d_To_%d.bytes", lasName, pointer, maxP);

                LASConverter.save2DMatrixToBinary(outFN, xyzClass(pointer:maxP,:));

                pointer = maxP;
            end
        end
        
        function LASData2Bytes_Voxels(xyzClass, lasName, pointsPerFile)
            LASConverter.resetFolder(lasName);
            
            [voxelLinearIndex, voxelIDs] = LASConverter.computeOptimalGrid( ...
                xyzClass(:,1), xyzClass(:,2), xyzClass(:,3), pointsPerFile);
            
            
            for id = voxelIDs'
                outFN = sprintf("%s/Points_Voxel_%d.bytes", lasName, id);
                ps = voxelLinearIndex == id;
                LASConverter.save2DMatrixToBinary(outFN, xyzClass(ps,:));
            end
        end
        
        
        function voxelIndex = LASData2Bytes_Octree(xyzClass, lasName, folder, pointsPerFile, initIndex, clearFolder)
            
            function [pc1, pc2, pc3, pc4, pc5, pc6, pc7, pc8] = splitInOctree(pc)
                x = pc(:,1);
                y = pc(:,2);
                z = pc(:,3);
                cx = (max(x) + min(x)) / 2;
                cy = (max(y) + min(y)) / 2;
                cz = (max(z) + min(z)) / 2;

                px = x > cx;
                py = y > cy;
                pz = z > cz;

                pc1 = px & py & pz;
                pc2 = px & py & ~pz;
                pc3 = px & ~py & pz;
                pc4 = px & ~py & ~pz;
                pc5 = ~px & py & pz;
                pc6 = ~px & py & ~pz;
                pc7 = ~px & ~py & pz;
                pc8 = ~px & ~py & ~pz;
            end
            
            function voxelIndex = saveOctree(xyzClass, folder, pointsPerFile, voxelName, indices)
                if isempty(xyzClass)
                    voxelIndex = {};
                    return
                end
                
                %Test
%                 if nNodes > 5
%                     voxelIndex = {};
%                     return
%                 end
                
                voxelIndex = struct();
                voxelIndex.filename = "";
                voxelIndex.min = min(xyzClass(:, 1:3), [], 1);
                voxelIndex.max = max(xyzClass(:, 1:3), [], 1);
                voxelIndex.indices = indices;    
                voxelIndex.children = {};
                
                if length(voxelIndex.min) ~= 3
                    disp("Error");
                end

                if length(xyzClass) < pointsPerFile
                    outFN = sprintf("%s/%s.bytes", folder, voxelName);
                    LASConverter.save2DMatrixToBinary(outFN, xyzClass);
                    voxelIndex.filename = sprintf("%s.bytes", voxelName);
                    
                    nNodes = nNodes + 1;
                else
                    [pc1, pc2, pc3, pc4, pc5, pc6, pc7, pc8] = splitInOctree(xyzClass); 
                    voxels = {pc1, pc2, pc3, pc4, pc5, pc6, pc7, pc8};
                    
                    voxelIndex.filename = "";
                    for i = 1:length(voxels)
                        newVoxelName = sprintf("%s_%d", voxelName, i);
                        xyzClassI = xyzClass(voxels{i}, :);
%                         fprintf("Processing Cloud with %d points\n", length(xyzClassI));
                        newVI = saveOctree(xyzClassI, folder, pointsPerFile, newVoxelName, [indices i]);
                        if ~isempty(newVI)
                            voxelIndex.children = {voxelIndex newVI};
                        end
                    end
                end
            end
            
            nNodes = 0;
            if clearFolder
                LASConverter.resetFolder(folder);
            end
            voxelIndex = saveOctree(xyzClass, folder, pointsPerFile, lasName, initIndex);
        end
        
        function PointCloudFiles2BinaryOctree(filenames, modelName, pointsPerFile)
            
            LASConverter.resetFolder(modelName);
            pMin = nan;
            voxelIndex = [];
            for lasIndex = 1:length(filenames)
                
                filename = filenames{lasIndex};
                load(filename);
                
                %Compute min of first
                if isnan(pMin)
                    pMin = [ min(points(:,1)), min(points(:,2)), min(points(:,3))];
                end
                
                %Bringing cloud to zero
                xyzClass = [points(:,1) - pMin(1), points(:,2) - pMin(2), points(:,3) - pMin(3), points(:,5)];
                newVoxelIndex = LASConverter.LASData2Bytes_Octree(xyzClass, filename, modelName, pointsPerFile, lasIndex, false);
                voxelIndex = [voxelIndex, newVoxelIndex];
            end
            
            viFile = sprintf("%s/voxelIndex.json", modelName);
            LASConverter.saveJSON(viFile, voxelIndex);
        end
        
        function LASFiles2BinaryOctree(filenames, modelName, pointsPerFile)
            
            LASConverter.resetFolder(modelName);
            pMin = nan;
            voxelIndex = [];
            for lasIndex = 1:length(filenames)
                
                filename = filenames{lasIndex};
                
                las = lasdata(filename);
                %Compute min of first
                if isnan(pMin)
                    pMin = [ min(las.x), min(las.y), min(las.z)];
                end
                
                %Bringing cloud to zero
                class = las.get_classification();
                xyzClass = [las.x - pMin(1), las.y - pMin(2), las.z - pMin(3), single(class)];
                clear las;
                clear class;
                
%                 maxProcessedPoints = 3*10^6; %Max 3 millions
%                 index = 1;
%                 while index <= length(xyzClass)
%                     nextIndex = index + maxProcessedPoints;
%                     if nextIndex > length(xyzClass)
%                         nextIndex = length(xyzClass);
%                     end
%                     fn = sprintf("%s_%d_%d__", filename, index, nextIndex);
%                     newVoxelIndex = LASConverter.LASData2Bytes_Octree(xyzClass(index:nextIndex, :), fn, modelName, pointsPerFile, lasIndex, false);
%                     voxelIndex = [voxelIndex, newVoxelIndex];
%                     index = nextIndex+1;
%                 end
                
                newVoxelIndex = LASConverter.LASData2Bytes_Octree(xyzClass, filename, modelName, pointsPerFile, lasIndex, false);
                voxelIndex = [voxelIndex, newVoxelIndex];
                
            end
            
            viFile = sprintf("%s/voxelIndex.json", modelName);
            LASConverter.saveJSON(viFile, voxelIndex);
        end
        
        function saveJSON(filename, data)
            json = jsonencode(data);
            
            json = strrep(json, ',', sprintf(',\r'));
            json = strrep(json, '[{', sprintf('[\r{\r'));
            json = strrep(json, '}]', sprintf('\r}\r]'));
            
            fid = fopen(filename,'wt');
            fprintf(fid, json);
            fclose(fid);
        end
        
        function resetFolder(folder)
            if exist(folder,'dir')
                rmdir(folder, 's');
            end
            mkdir(folder);
        end
        
        function save2DMatrixToBinary(filename, matrix)
            data = matrix(:)';
            data = [size(matrix), data]; %%First 2 numbers are dimensions

            data = single(data);
            data = typecast(data,'uint8');
            fileID = fopen(filename,'w');
            fwrite(fileID,data);
            fclose(fileID);
        end
        
        
    end
end