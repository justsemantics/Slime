using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class Slime : MonoBehaviour
{
    #region private variables
    
    private bool Initialized = false;

    [SerializeField]
    private ComputeShader computeShader;

    private ComputeBuffer actorBuffer, speciesBuffer, settingsBuffer;

    private RenderTexture drawTexture, trailTexture, trailTexture2;

    [SerializeField]
    private Texture2D flowTexture, angleAdjustmentTexture;

    private Settings currentSettings;
    private Species[] species;
    private Actor[] actors;

    //parameters
    [SerializeField]
    private int resolution, numSpecies, numActors;

    [SerializeField]
    [Range(0, 30)]
    private float sensorSizeMin, sensorSizeMax;

    [SerializeField]
    [Range(0f, 1000f)]
    private float
        moveSpeedMin,
        moveSpeedMax,
        flowSpeedMin,
        flowSpeedMax;

    [SerializeField]
    [Range(0f, 100f)]
    private float
        sensorAngleMin,
        sensorAngleMax,
        sensorDistanceMin,
        sensorDistanceMax,
        turnSpeedMin,
        turnSpeedMax;

    [SerializeField]
    [Range(0f, 10f)]
    private float
        intentionalTurnWeightMin,
        intentionalTurnWeightMax,
        randomTurnWeightMin,
        randomTurnWeightMax;

    [SerializeField]
    [Range(0f, 1f)]
    private float angleAdjustmentWeight;


    [SerializeField]
    private float evaporateSpeed;

    #endregion

    #region public methods
    //haha
    #endregion

    #region unity methods

    // Start is called before the first frame update
    private void Start()
    {
        initialize();
    }

    // Update is called once per frame
    private void Update()
    {
        if (Initialized)
        {
            //these values change every frame
            computeShader.SetFloat("deltaTime", Time.deltaTime);
            computeShader.SetFloat("time", Time.time);

            //run main sim
            computeShader.Dispatch(0, numActors / 64, 1, 1);

            //blur the trail texture in two passes
            computeShader.Dispatch(1, resolution / 8, resolution / 8, 1);
            computeShader.Dispatch(2, resolution / 8, resolution / 8, 1);
        }
    }

    /// <summary>
    /// handle changes from within the editor window
    /// </summary>
    private void OnValidate()
    {
        if (Initialized && UnityEditor.EditorApplication.isPlaying)
        {
            UpdateSettings();
        }
    }
    #endregion

    #region private methods

    private void initialize()
    {
        //set up textures
        trailTexture = new RenderTexture(resolution, resolution, 0);
        trailTexture.enableRandomWrite = true;
        trailTexture.filterMode = FilterMode.Point;
        trailTexture.wrapMode = TextureWrapMode.Mirror;

        drawTexture = new RenderTexture(trailTexture);
        trailTexture2 = new RenderTexture(trailTexture);

        //set up buffers
        actorBuffer = new ComputeBuffer(numActors, sizeof(int) + 3 * sizeof(float));
        actors = createActorsCircle(0.5f);
        actorBuffer.SetData(actors);

        speciesBuffer = new ComputeBuffer(numSpecies, sizeof(int) * 2 + sizeof(float) * 15);
        species = createSpecies();
        speciesBuffer.SetData(species);

        settingsBuffer = new ComputeBuffer(1, sizeof(float) * 17);
        UpdateSettings();

        //pass textures to compute shader
        computeShader.SetTexture(0, "_trailTexToWrite", trailTexture);
        computeShader.SetTexture(0, "_trailTexToWrite2", trailTexture2);
        computeShader.SetTexture(0, "_flowTexture", flowTexture);
        computeShader.SetTexture(0, "_angleAdjustmentTexture", angleAdjustmentTexture);

        computeShader.SetTexture(1, "_trailTexToWrite", trailTexture);
        computeShader.SetTexture(1, "_trailTexToSample", trailTexture2);

        computeShader.SetTexture(2, "_trailTexToWrite", trailTexture2);
        computeShader.SetTexture(2, "_trailTexToSample", trailTexture);

        //pass parameters and buffers to shader
        computeShader.SetInt("resolution", resolution);
        computeShader.SetFloat("evaporateSpeed", evaporateSpeed);

        computeShader.SetBuffer(0, "actors", actorBuffer);
        computeShader.SetBuffer(0, "species", speciesBuffer);
        computeShader.SetBuffer(0, "settingsBuffer", settingsBuffer);

        //set up renderer to draw the results
        Renderer r = GetComponent<Renderer>();
        r.material.SetTexture("_MainTex", trailTexture2);
        r.material.SetTexture("_MetallicGlossMap", trailTexture2);
        r.material.SetTexture("_SmoothnessTextureChannel", trailTexture2);

        //done initialization, ready to update
        Initialized = true;
    }

    /// <summary>
    /// when public variables are changed, create a new Settings struct and push it to the GPU
    /// </summary>
    private void UpdateSettings()
    {
        currentSettings = getSettings();

        settingsBuffer.SetData(new Settings[] { currentSettings });
    }

    /// <summary>
    /// Returns Settings struct created from the public class variables
    /// </summary>
    /// <returns></returns>
    private Settings getSettings()
    {
        Settings newSettings = new Settings(
            new Vector2[] {
                //each Vector2's x value is the minimum possible value
                //each Vector2's y value is the range of values available
                new Vector2(sensorSizeMin, sensorSizeMax - sensorSizeMin),
                new Vector2(sensorAngleMin, sensorAngleMax - sensorSizeMin),
                new Vector2(sensorDistanceMin, sensorDistanceMax - sensorDistanceMin),
                new Vector2(moveSpeedMin, moveSpeedMax - moveSpeedMin),
                new Vector2(flowSpeedMin, flowSpeedMax - flowSpeedMin),
                new Vector2(turnSpeedMin, turnSpeedMax - turnSpeedMin),
                new Vector2(intentionalTurnWeightMin, intentionalTurnWeightMax - intentionalTurnWeightMin),
                new Vector2(randomTurnWeightMin, randomTurnWeightMax - randomTurnWeightMin)
            },
            new float[]
            {
                angleAdjustmentWeight
            });

        return newSettings;
    }

    /// <summary>
    /// Creates an array of numSpecies randomly generated Species
    /// </summary>
    /// <returns></returns>
    private Species[] createSpecies()
    {
        Species[] createdSpecies = new Species[numSpecies];
        for (int i = 0; i < numSpecies; i++)
        {
            //color is evenly spread around the color wheel
            Color color = Color.HSVToRGB((float)i / numSpecies, 1, 1);

            Color inverseColor = Vector4.one - (Vector4)color;

            //all other parameters are random between 0-1
            Species s = new Species(
                _index: i,
                _sensorSize: UnityEngine.Random.value,
                _sensorAngle: UnityEngine.Random.value,
                _sensorDistance: UnityEngine.Random.value,
                _moveSpeed: UnityEngine.Random.value,
                _flowSpeed: UnityEngine.Random.value,
                _turnSpeed: UnityEngine.Random.value,
                _intentionalTurnWeight: UnityEngine.Random.value,
                _randomTurnWeight: UnityEngine.Random.value,
                _color: color,
                _inverseColor: inverseColor);

            createdSpecies[i] = s;
        }

        return createdSpecies;
    }

    /// <summary>
    /// creates numActors Actors randomly distributed on the texture
    /// </summary>
    /// <returns></returns>
    private Actor[] createActors()
    {
        Actor[] createdActors = new Actor[numActors];
        for (int i = 0; i < numActors; i++)
        {
            int species = i % numSpecies;
            Vector2 position = new Vector2(
                UnityEngine.Random.value * resolution,
                UnityEngine.Random.value * resolution);
            float angle = UnityEngine.Random.Range(0, 2 * Mathf.PI);

            Actor a = new Actor(species, position, angle);

            createdActors[i] = a;
        }

        return createdActors;
    }

    /// <summary>
    /// creates numActors Actors in a circle. A radius of 0.5 will touch the sides
    /// </summary>
    /// <returns></returns>
    private Actor[] createActorsCircle(float radius)
    {
        Actor[] createdActors = new Actor[numActors];
        for (int i = 0; i < numActors; i++)
        {
            int species = i % 10;
            Vector2 position = ((UnityEngine.Random.insideUnitCircle) + Vector2.one) * radius * resolution;
            float angle = UnityEngine.Random.Range(0, 2 * Mathf.PI);

            Actor a = new Actor(species, position, angle);

            createdActors[i] = a;
        }

        return createdActors;
    }

    #endregion
}


public struct Settings
{
    public Settings(Vector2[] ranges, float[] weights)
    {
        sensorSizeRange = ranges[0];
        sensorAngleRange = ranges[1];
        sensorDistanceRange = ranges[2];
        moveSpeedRange = ranges[3];
        flowSpeedRange = ranges[4];
        turnSpeedRange = ranges[5];
        intentionalTurnRange = ranges[6];
        randomTurnRange = ranges[7];

        angleAdjustmentWeight = weights[0];
    }

    public Vector2 sensorSizeRange;
    public Vector2 sensorAngleRange;
    public Vector2 sensorDistanceRange;
    public Vector2 moveSpeedRange;
    public Vector2 flowSpeedRange;
    public Vector2 turnSpeedRange;
    public Vector2 intentionalTurnRange;
    public Vector2 randomTurnRange;

    public float angleAdjustmentWeight;
}

public struct Species
{
    public Species(
        int _index,
        float _sensorSize,
        float _sensorAngle,
        float _sensorDistance,
        float _moveSpeed,
        float _flowSpeed,
        float _turnSpeed,
        float _intentionalTurnWeight,
        float _randomTurnWeight,
        Vector4 _color,
        Vector4 _inverseColor)
    {
        index = _index;
        sensorSize = _sensorSize;
        sensorAngle = _sensorAngle;
        sensorDistance = _sensorDistance;
        moveSpeed = _moveSpeed;
        flowSpeed = _flowSpeed;
        turnSpeed = _turnSpeed;
        intentionalTurnWeight= _intentionalTurnWeight;
        randomTurnWeight = _randomTurnWeight;
        color = _color;
        inverseColor = _inverseColor;
    }

    public int index;
    public float sensorSize;
    public float sensorAngle;
    public float sensorDistance;
    public float moveSpeed;
    public float flowSpeed;
    public float turnSpeed;
    public float intentionalTurnWeight;
    public float randomTurnWeight;
    public Vector4 color;
    public Vector4 inverseColor;
}

public struct Actor
{
    public Actor(
        int _species,
        Vector2 _position,
        float _angle)
    {
        species = _species;
        position = _position;
        angle = _angle;
    }

    public int species;
    public Vector2 position;
    public float angle;
}

