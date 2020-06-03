using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Matrix2D {

    public readonly float[,] values = null;

    public Matrix2D(float[,] values){
        this.values = values;
    }

    private static float[] bytes2Float(byte[] bytes){

        float[] fs = new float[bytes.Length / 4];
        for (int i = 0, j = 0; i < bytes.Length; i+=4, j++)
        {
            fs[j] = System.BitConverter.ToSingle(bytes, i);
        }
        return fs;

    }

    [Obsolete]
    public static Matrix2D readFromBytes(byte[] bytes){

        float[] buffer = bytes2Float(bytes);
        int nRows = (int)buffer[0];
        int nCols = (int)buffer[1];
        float[,] values = new float[(int)buffer[0], (int)buffer[1]];

        int offset = 2;
        for (int i = 0; i < nRows; i++)
        {
            for (int j = 0; j < nCols; j++){
                values[i, j] = buffer[offset];
                offset+=1;
            }
        }

        //for (int i = 0; i < nRows; i++)
        //{
        //    Debug.Log("" + values[i, 0] + " " + values[i, 1] + " " + values[i, 2] + " " + values[i, 3]);
        //}

        //Debug.Log(values);

        return new Matrix2D(values);
    }

    public static float[,] ReadFromBytes(byte[] bytes)
    {

        float[] buffer = bytes2Float(bytes);
        int nRows = (int)buffer[0];
        int nCols = (int)buffer[1];
        float[,] values = new float[(int)buffer[0], (int)buffer[1]];

        int offset = 2;
        for (int i = 0; i < nRows; i++)
        {
            for (int j = 0; j < nCols; j++)
            {
                values[i, j] = buffer[offset];
                offset += 1;
            }
        }

        //for (int i = 0; i < nRows; i++)
        //{
        //    Debug.Log("" + values[i, 0] + " " + values[i, 1] + " " + values[i, 2] + " " + values[i, 3]);
        //}

        //Debug.Log(values);

        return values;
    }
}
