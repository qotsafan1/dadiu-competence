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

    Vector3[] framePositionData = new Vector3[4330];
    float[] frameRotationData = new float[4330];

    List<int> lastFrames = new List<int>();

    float animN;

    int startFrame = 3;

    bool rotationLoaded = false;

    Vector3 futureWantedPosition;
    private int loopFrames;

    private void Awake()
    {
        LoadPositionData();        
    }

    // Use this for initialization
    void Start()
    {
        currentFrame = startFrame;
        animN = startFrame;

        // CalculateAllBestNeighbours();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        DrawFuturePosition();

        PlayAnim();

        
        // SavePositionData();
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
            Debug.DrawLine(nextPosition[i], nextPosition[i - 1], Color.red);
        }

        futureWantedPosition = nextPosition[nextPosition.Count - 1];
    }

    private void PlayAnim()
    {
        bool calculationRunning = false;

        if (loopFrames == 30)
        {
            animN = CalculateFrame();
            loopFrames = 0;
            calculationRunning = true;
        }
        else
        {
            loopFrames++;
        }

        PlaySpecificFrame(animN);

        DrawTrajectoryOfCurrentFrame(Convert.ToInt32(animN), Color.red);

        if (!rotationLoaded)
        {
            var angle = Quaternion.Euler(0, frameRotationData[startFrame], 0);
            transform.rotation = angle;
            rotationLoaded = true;
        }

        if (!calculationRunning)
        {
            animN += 1;
        }
        else
        {
            calculationRunning = true;
        }
    }

    private float CalculateFrame()
    {
        currentFrame = Convert.ToInt32(animN);
        var neighbours = bestNeighbours[animN.ToString()];

        float bestFinish = 100000f;
        int bestNextFrame = 3;
        
        for (var i=0; i<100; i++)
        {
            Vector3 lastPos = DrawTrajectoryOfCurrentFrame(Convert.ToInt32(neighbours[i.ToString()]), Color.black);
            float temptFinish = Vector3.Distance(futureWantedPosition, lastPos);

            if (temptFinish < bestFinish)
            {
                bestFinish = temptFinish;
                bestNextFrame = i;
            }
        }              

        return (float)bestNextFrame;
    }

    private void PlaySpecificFrame(float frameNumber)
    {
        Animator anim = GetComponent<Animator>();
        anim.speed = 1;
        float frame = (1f / 4329) * animN;
        anim.Play("walk", 0, frame);
    }

    private Vector3 DrawTrajectoryOfCurrentFrame(int currentFrame, Color color)
    {
        Animator anim = GetComponent<Animator>();        
        Vector3 lastPos = transform.position;

        for (var i = currentFrame; i < currentFrame + 30; i++)
        {
            Vector3 differenceVector = framePositionData[i + 1] - framePositionData[i];
            Vector3 newVector = lastPos + differenceVector;
            
            Debug.DrawLine(lastPos, newVector, color);

            lastPos = newVector;
        }

        return lastPos;
    }

    private double calculateCost(int currentFrame, int futureFrame)
    {
        var currentRoot = data["data"][0]["angles"][currentFrame];
        var futureRoot = data["data"][0]["angles"][futureFrame];
        var currentData = data["data"][1]["angles"][currentFrame];
        var futureData = data["data"][1]["angles"][futureFrame];

        Vector3 currentPositionRoot = new Vector3(currentRoot[0].ToObject<float>(), currentRoot[1].ToObject<float>(), currentRoot[2].ToObject<float>());
        Vector3 futurePositionRoot = new Vector3(futureRoot[0].ToObject<float>(), futureRoot[1].ToObject<float>(), futureRoot[2].ToObject<float>());

        var velocityRoot = (currentPositionRoot - futurePositionRoot).magnitude / Time.deltaTime;
        var angleRoot = Vector3.Angle(currentPositionRoot, futurePositionRoot);

        Vector3 currentPositionBone = currentPositionRoot + new Vector3(9.128f, 0f, 0.019f);
        Vector3 futurePositionBone = futurePositionRoot + new Vector3(9.128f, 0f, 0.019f);

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
        data = JObject.Parse(File.ReadAllText(@"C:\Users\qotsafan1\Documents\Competence\dadiu-competence\Offline\asdf.json"));
        positions = JObject.Parse(File.ReadAllText(@"C:\Users\qotsafan1\Documents\Competence\dadiu-competence\Competence\positions.json"));
        bestNeighbours = JObject.Parse(File.ReadAllText(@"C:\Users\qotsafan1\Documents\Competence\dadiu-competence\Competence\bestNeighbours.json"));

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
        
        if (animN < 4330)
        {            
            Animator anim = GetComponent<Animator>();
            /*
            anim.speed = 1;
            float frame = (1f / 4329) * animN;
            anim.Play("walk", 0, frame);
            */
            positions.Add(animN.ToString(), anim.transform.position.x.ToString() + "," + anim.transform.position.z.ToString() + "," + anim.transform.rotation.eulerAngles.y.ToString());
        }

        if (animN == 4330)
        {
            File.WriteAllText("positions.json", positions.ToString());
        }

        animN++;
    }


    private void CalculateAllBestNeighbours()
    {
        bestNeighbours = new JObject();

        for (var i = startFrame; i < 4260; i++)
        {
            float[] bestFrames = CalculateBestNext(i);

            JObject best = new JObject();
            for (var j = 0; j < bestFrames.Length; j++)
            {
                best.Add(j.ToString(), bestFrames[j]);
            }

            bestNeighbours.Add(i.ToString(), best);
        }

        File.WriteAllText("bestNeighbours.json", bestNeighbours.ToString());
    }
    private float[] CalculateBestNext(float frame)
    {
        float[] lowestFrame = new float[100] { 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000 };
        double[] lowestCost = new double[100] { 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000, 10000000 };
        int currentFrame = Convert.ToInt32(frame);

        for (var i = startFrame; i < 4260; i++)
        {
            if (currentFrame != i && (currentFrame - i != 1) && (currentFrame - i != 2) && (currentFrame - i != 3) && (currentFrame - i != 4) && (currentFrame - i != 5) 
                && (currentFrame - i != 6) && (currentFrame - i != 7) && (currentFrame - i != 8) && (currentFrame - i != 9) && (currentFrame - i != 10))
            {
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
        }

        return lowestFrame;
    }
}