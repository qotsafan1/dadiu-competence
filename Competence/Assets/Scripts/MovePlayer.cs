using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.IO;
using System;

public class MovePlayer : MonoBehaviour
{
    public GameObject prediction;    

    private int currentFrame;    

    private List<KeyValuePair<int, Vector3>> framePos = new List<KeyValuePair<int, Vector3>>();

    JObject positions;

    JObject data;

    JObject bestNeighbours;

    Vector3[] framePositionData;
    float[] frameRotationData;

    List<int> lastFrames = new List<int>();

    int totalFrames = 0;

    float animN;

    int startFrame = 4;

    int lastCalculatedFrame;

    bool rotationLoaded = false;

    Vector3[] futureWantedPosition;
    private int loopFrames;

    private void Awake()
    {
        LoadPositionData();        
    }

    // Use this for initialization
    void Start()
    {
        currentFrame = startFrame;
        lastCalculatedFrame = startFrame;
        animN = startFrame;
        futureWantedPosition = new Vector3[3] { transform.position, transform.position, transform.position };

        // positions = new JObject();
        // CalculateAllBestNeighbours();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
       DrawFuturePosition();
       DrawPossibilities();

       PlayAnim();
        
       
        // SavePositionData();
    }

    private void PlayAnim()
    {
        if (!rotationLoaded)
        {
            var angle = Quaternion.Euler(0, frameRotationData[startFrame], 0);
            transform.rotation = angle;
            rotationLoaded = true;
        }

        PlaySpecificFrame(animN);        

        DrawTrajectoryOfCurrentFrame(Convert.ToInt32(animN), Color.red, 30);

        
        Debug.Log(animN);
        if (loopFrames == 30)
        {
            Debug.Log("Calculating");
            animN = CalculateFrame();            
            loopFrames = 0;
        }
        else
        {
            loopFrames++;
            animN += 1;
        }
        
    }

    private Vector3 CalculateFuturePosition(int frameNumber)
    {
        prediction.transform.position = transform.position;
        prediction.transform.rotation = transform.rotation;

        var x = Input.GetAxis("Horizontal") * Time.deltaTime * 50.0f * frameNumber;
        var z = Input.GetAxis("Vertical") * Time.deltaTime * 0.5f * frameNumber;

        prediction.transform.Rotate(0, x, 0);
        prediction.transform.Translate(0, 0, z);

        return prediction.transform.position;
    }

    private void DrawFuturePosition()
    {
        List<Vector3> nextPosition = new List<Vector3>();

        for (var i = 1; i <= 30; i++)
        {
            nextPosition.Add(CalculateFuturePosition(i));
        }

        for (var i = 1; i < nextPosition.Count; i++)
        {
            Debug.DrawLine(nextPosition[i], nextPosition[i - 1], Color.blue);
        }

        futureWantedPosition[0] = nextPosition[nextPosition.Count - 21];
        futureWantedPosition[1] = nextPosition[nextPosition.Count - 11];
        futureWantedPosition[2] = nextPosition[nextPosition.Count - 1];
    }

    private float CalculateFrame()
    {
        currentFrame = Convert.ToInt32(animN);
        var neighbours = bestNeighbours[animN.ToString()];

        float bestFinish = 100000f;
        int bestNextFrame = 3;

        for (var i=0; i<300; i++)
        {
            int loopedFrame = Convert.ToInt32(neighbours[i.ToString()]);

            if (loopedFrame + 62 > (totalFrames - 1) 
                || loopedFrame == currentFrame 
            ) {
                continue;
            }

            Vector3[] lastPos = GetBestTrajectoryOfCurrentFrame(loopedFrame);

            float temptFinish = Vector3.Distance(futureWantedPosition[0], lastPos[0]) 
                + Vector3.Distance(futureWantedPosition[1], lastPos[1]) 
                + 2*Vector3.Distance(futureWantedPosition[2], lastPos[2]);

            if (temptFinish < bestFinish)
            {
                bestFinish = temptFinish;
                bestNextFrame = i;
            }
        }

        lastCalculatedFrame = Convert.ToInt32(neighbours[bestNextFrame.ToString()]);
 
        return float.Parse(neighbours[bestNextFrame.ToString()].ToString());
    }

    private void PlaySpecificFrame(float frameNumber)
    {
        Animator anim = GetComponent<Animator>();
        anim.speed = 2;
        float frame = (1f / ((totalFrames-1))) * animN;
        anim.Play("large-w-circle", 0, frame);
    }

    private void DrawPossibilities()
    {
        currentFrame = Convert.ToInt32(animN);

        var neighbours = bestNeighbours[animN.ToString()];

        for (var i = 0; i < 300; i++)
        {
            if (Convert.ToInt32(neighbours[i.ToString()])+40 > totalFrames-1)
            {
                continue;
            }

            DrawTrajectoryOfCurrentFrame(Convert.ToInt32(neighbours[i.ToString()]), Color.black, 100);            
        }
    }

    private void DrawTrajectoryOfCurrentFrame(int currentFrame, Color color, int amountOfFrames)
    {
        Animator anim = GetComponent<Animator>();        
        Vector3 lastPos = anim.transform.position;

        // var startRotation = transform.eulerAngles.y - frameRotationData[Convert.ToInt32(animN)];
        var startRotation = transform.rotation.eulerAngles.y - frameRotationData[currentFrame];
        
        for (var i = currentFrame; i < currentFrame + amountOfFrames; i++)
        {
            if (i > framePositionData.Length-5)
            {
                continue;
            }
            Vector3 first = Quaternion.Euler(0, startRotation, 0) * framePositionData[i + 1];
            Vector3 second = Quaternion.Euler(0, startRotation, 0) * framePositionData[i];
            Vector3 differenceVector = first - second;


            // Vector3 differenceVector = framePositionData[i + 1] - framePositionData[i];
            Vector3 newVector = lastPos + differenceVector;
            
            Debug.DrawLine(lastPos, newVector, color);

            lastPos = newVector;
        }
    }

    private Vector3[] GetBestTrajectoryOfCurrentFrame(int currentFrame)
    {
        Vector3 lastPos = transform.position;
        Vector3[] threePos = new Vector3[3];

        var startRotation = transform.rotation.eulerAngles.y - frameRotationData[currentFrame];

        for (var i = currentFrame; i < currentFrame + 30; i++)
        {
            Vector3 first = Quaternion.Euler(0, startRotation, 0) * framePositionData[i + 1];
            Vector3 second = Quaternion.Euler(0, startRotation, 0) * framePositionData[i];
            Vector3 differenceVector = first - second;
            Vector3 newVector = lastPos + differenceVector;

            lastPos = newVector;

            if (i == currentFrame + 9)
            {
                threePos[0] = newVector;
            }
            else if (i == currentFrame + 19)
            {
                threePos[1] = newVector;
            }
            else if (i == currentFrame + 29)
            {
                threePos[2] = newVector;
            }
        }

        return threePos;
    }

    private double calculateCost(int currentFrame, int futureFrame)
    {
        var currentRoot = data["data"][0]["angles"][currentFrame];
        var futureRoot = data["data"][0]["angles"][futureFrame];        

        Vector3 currentPositionRoot = new Vector3(currentRoot[0].ToObject<float>(), currentRoot[1].ToObject<float>(), currentRoot[2].ToObject<float>());
        Vector3 futurePositionRoot = new Vector3(futureRoot[0].ToObject<float>(), futureRoot[1].ToObject<float>(), futureRoot[2].ToObject<float>());

        var velocityRoot = (currentPositionRoot - futurePositionRoot).magnitude / Time.deltaTime;
        var angleRoot = Vector3.Angle(currentPositionRoot, futurePositionRoot);

        Vector3 currentPositionBone = currentPositionRoot + new Vector3(9.374f, 0f, 0.020f);
        Vector3 futurePositionBone = futurePositionRoot + new Vector3(9.374f, 0f, 0.020f);

        var velocityBone = (currentPositionBone - futurePositionBone).magnitude / Time.deltaTime;
        var angleBone = Vector3.Angle(currentPositionBone, futurePositionBone);

        double value = Math.Sqrt(Math.Pow(Convert.ToDouble(velocityRoot), 2) +
                    Math.Pow(Convert.ToDouble(angleRoot), 2) +
                    Math.Pow(Convert.ToDouble(velocityBone), 2) +
                    Math.Pow(Convert.ToDouble(angleBone), 2));

        return value;
    }

    public void LoadPositionData()
    {
        data = JObject.Parse(File.ReadAllText(@"C:\Users\qotsafan1\Documents\Competence\dadiu-competence\Offline\large-w-circle.json"));

        foreach (var d in data["data"][0]["angles"])
        {
            totalFrames++;
        }
        Debug.Log("Totalframes: " + totalFrames);
    
        positions = JObject.Parse(File.ReadAllText(@"C:\Users\qotsafan1\Documents\Competence\dadiu-competence\Competence\positions_large-w-circle.json"));
        bestNeighbours = JObject.Parse(File.ReadAllText(@"C:\Users\qotsafan1\Documents\Competence\dadiu-competence\Competence\bestNeighbours_large-w-circle.json"));

        frameRotationData = new float[totalFrames];
        framePositionData = new Vector3[totalFrames];

        for (int i = 0; i < positions.Count; i++)
        {
            string[] sArray = positions[i.ToString()].ToString().Split(',');

            framePositionData[i] = new Vector3(float.Parse(sArray[0]), 0, float.Parse(sArray[2]));
            frameRotationData[i] = float.Parse(sArray[1]);
        }
        
        //Debug.Log(bestNeighbours);
    }

    public void SavePositionData()
    {
        
        if (animN < totalFrames)
        {            
            //Animator anim = GetComponent<Animator>();
            //anim.speed = 1;
            //float frame = (1f / (totalFrames-1)) * animN;
            //anim.Play("large-w-circle", 0, frame);            
            positions.Add(animN.ToString(), transform.position.x.ToString() + "," + transform.position.z.ToString() + "," + transform.rotation.eulerAngles.y.ToString());
        }

        if (animN == totalFrames)
        {
            File.WriteAllText("positions_large-w-circle.json", positions.ToString());
        }

        animN++;
    }

    private void CalculateAllBestNeighbours()
    {
        bestNeighbours = new JObject();

        for (var i = startFrame; i < totalFrames; i++)
        {
            float[] bestFrames = CalculateBestNext(i);

            JObject best = new JObject();
            for (var j = 0; j < bestFrames.Length; j++)
            {
                best.Add(j.ToString(), bestFrames[j]);
            }

            bestNeighbours.Add(i.ToString(), best);
        }

        File.WriteAllText("bestNeighbours_large-w-circle.json", bestNeighbours.ToString());
    }

    private float[] CalculateBestNext(float frame)
    {
        var numOfNeighbours = 300;
        float[] lowestFrame = new float[numOfNeighbours];
        double[] lowestCost = new double[numOfNeighbours];

        for (var k=0; k<numOfNeighbours; k++)
        {
            lowestFrame[k] = 10000000000000;
            lowestCost[k] = 10000000000000;
        }

        int currentFrame = Convert.ToInt32(frame);

        for (var i = startFrame; i < totalFrames; i++)
        {
            if (currentFrame == i 
                || (currentFrame > i && (currentFrame - i) < 11) 
                || (currentFrame < i && (i-currentFrame) < 11)
            ) {
                continue;
            }

            double cost = calculateCost(currentFrame, i);

            double highestCost = 0;
            var highestCostPos = 0;

            for (var j = 0; j < lowestCost.Length; j++)
            {
                if (highestCost < lowestCost[j])
                {
                    highestCost = lowestCost[j];
                    highestCostPos = j;
                }
            }

            if (cost < highestCost)
            {
                lowestCost[highestCostPos] = cost;
                lowestFrame[highestCostPos] = i;
            }            
        }

        return lowestFrame;
    }
}