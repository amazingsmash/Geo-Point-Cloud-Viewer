using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasPointCloudListener : MonoBehaviour, IPointCloudListener
{
    TCPServer server = null;

    public Text screenText = null;

    public GameObject selectedPointMark = null;


    public void onPointSelected(Vector3 point, float classCode)
    {
        //GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //sphere.transform.position = point;
        //float ballSize = 2.0f;
        //sphere.transform.localScale = new Vector3(ballSize, ballSize, ballSize);

        if (selectedPointMark != null)
        {
            Instantiate(selectedPointMark, point, Quaternion.identity);
        }

        SelectPointCmd selectPointCmd = new SelectPointCmd(0, 0, 0);

        int code = (int)classCode;
        string codeMsg = " - Class ";
        switch (code)
        {
            case 17:
                {
                    codeMsg += "Catenary";
                    break;
                }
            case 3:
                {
                    codeMsg += "Ground 1";
                    break;
                }
            case 23:
                {
                    codeMsg += "Ground 2";
                    break;
                }
            case 16:
                {
                    codeMsg += "Turret";
                    break;
                }
            default:
                {
                    codeMsg += "None";
                    break;
                }

        }

        string msg = "Point Selected: " + point.ToString() + codeMsg;

        screenText.text = msg;
    }

}
