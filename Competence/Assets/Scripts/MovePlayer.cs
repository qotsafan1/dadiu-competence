using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.IO;
using System;

public class MovePlayer : MonoBehaviour
{

    public float speed = 6.00F;
    public float jumpSpeed = 8.0F;
    public float gravity = 20.0F;
    public float rotateSpeed = 3.0F;
    private Vector3 moveDirection = Vector3.zero;
    
    public GameObject prediction;

    private int currentFrame;

    private List<Vector3> oldPosition = new List<Vector3>();

    JObject data;

    List<int> lastFrames = new List<int>();

    private void Awake()
    {
        data = JObject.Parse(File.ReadAllText(@"C:\Users\qotsafan1\Documents\Competence\dadiu-competence\Offline\asdf.json"));
        Debug.Log(data["data"][1]["angles"]);        
    }

    // Use this for initialization
    void Start()
    {
        currentFrame = 1000;
    }

    // Update is called once per frame
    void Update()
    {
        oldPosition.Add(new Vector3(transform.position.x, transform.position.y-0.75f, transform.position.z));
        
        if (oldPosition.Count > 60)
        {
            oldPosition.RemoveAt(0);
        }

        var x = Input.GetAxis("Horizontal") * Time.deltaTime * 150.0f;
        var z = Input.GetAxis("Vertical") * Time.deltaTime * 150.0f;

        transform.Rotate(0, x, 0);
        transform.Translate(0, 0, z);
        
        DrawFuturePosition();
        PlayAnim();
    }

    private Vector3 CalculateFuturePosition(int frameNumber)
    {
        prediction.transform.position = transform.position;
        prediction.transform.rotation = transform.rotation;

        var x = Input.GetAxis("Horizontal") * Time.deltaTime * 150.0f * frameNumber;
        var z = Input.GetAxis("Vertical") * Time.deltaTime * 150.0f * frameNumber;

        prediction.transform.Rotate(0, x, 0);
        prediction.transform.Translate(0, 0, z);
        
        return new Vector3(prediction.transform.position.x, prediction.transform.position.y-75f, prediction.transform.position.z);
    }

    private void DrawFuturePosition()
    {
        List<Vector3> nextPosition = new List<Vector3>();

        for (var i=1; i<=10; i++)
        {            
            nextPosition.Add(CalculateFuturePosition(i));
        }

        for (var i = 0; i < nextPosition.Count; i++)
        {
            if (i == 0)
            {
                Debug.DrawLine(nextPosition[i], new Vector3(transform.position.x, transform.position.y - 75f, transform.position.z), Color.red);
                continue;
            }
            Debug.DrawLine(nextPosition[i], nextPosition[i - 1], Color.red);
        }        
    }

    private void PlayAnim()
    {
        var countFrames = 0;
        float minCost = 1000000;
        var minFrame = 10000000;        

        var leftFoot = data["data"][1]["angles"];
        var rightFoot = data["data"][2]["angles"];

        foreach (var array in leftFoot)
        {
            if (currentFrame != countFrames && !lastFrames.Contains(countFrames))
            {
                float cost = calculateCost(leftFoot[currentFrame], array) + calculateCost(rightFoot[currentFrame], rightFoot[countFrames]);

                if (cost < minCost)
                {
                    minCost = cost;
                    minFrame = countFrames;
                }
            }

            countFrames += 1;
        }

        lastFrames.Add(minFrame);
        if (lastFrames.Count > 50)
        {
            lastFrames.RemoveAt(0);
        }

        currentFrame = minFrame;

        Animator anim = GetComponent<Animator>();
        anim.speed = 1;
        float frame = (1f / 3440) * currentFrame;
        anim.Play("walk", 0, frame);
        //anim.Play()
    }

    private float calculateCost(JToken currentFrame, JToken futureFrame)
    {
        float value = Math.Abs(currentFrame[0].ToObject<float>() - futureFrame[0].ToObject<float>()) +
                    Math.Abs(currentFrame[1].ToObject<float>() - futureFrame[1].ToObject<float>()) +
                    Math.Abs(currentFrame[2].ToObject<float>() - futureFrame[2].ToObject<float>());

        return value;
    }
}