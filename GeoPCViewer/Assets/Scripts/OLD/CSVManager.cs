using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CSVManager {

    public static float[,] ReadFloatCSVMatrix(TextAsset csvFile)
    {
        return ReadFloatCSVMatrix(csvFile.text);
    }

    public static float[,] ReadFloatCSVMatrix(string csvString)
    {
        string[] lines = csvString.Split("\n"[0]);
        int nRows = lines.Length;

        if (lines[lines.Length - 1].Length == 0)
        {
            nRows--;
        }


        int nCol = lines[0].Split(',').Length;
        float[,] values = new float[nRows, nCol];
        for (int i = 0; i < nRows; i++)
        {
            string l = lines[i];
            string[] numStr = l.Split(',');

            if (numStr.Length == nCol) { 
                for (int j = 0; j < nCol; j++)
                {
                    try { values[i, j] = float.Parse(numStr[j]); }
                    catch (System.FormatException e)
                    {
                        Debug.Log("Problem parsing " + numStr[j]);
                    }

                }
            }
        }

        return values;
    }

    //static private string[] SplitCsvLine(string line)
    //{
    //    return (from System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(line,
    //        @"(((?<x>(?=[,\r\n]+))|""(?<x>([^""]|"""")+)""|(?<x>[^,\r\n]+)),?)",
    //        System.Text.RegularExpressions.RegexOptions.ExplicitCapture)
    //            select m.Groups[1].Value).ToArray();
    //}


}
