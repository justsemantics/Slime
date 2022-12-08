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
    SpeciesBufferValues defaultSpecies;

    [SerializeField]
    [Range(0, 30)]
    public int sensorSizeMin, sensorSizeMax;


    [SerializeField]
    [Range(0f, 300f)]
    float moveSpeedMin, moveSpeedMax;

    [SerializeField]
    [Range(0f, 45f)]
    float sensorAngleMin,
        sensorAngleMax,
        sensorDistanceMin,
        sensorDistanceMax,
        turnSpeedMin,
        turnSpeedMax;

    [SerializeField]
    int numActors;

    [SerializeField]
    ComputeShader computeShader;

    [SerializeField]
    int resolution;

    [SerializeField]
    float evaporateSpeed;

    [SerializeField]
    int numSpecies;

    Actor[] actors;
    SpeciesBufferValues[] species;

    RenderTexture drawTexture, trailTexture;
    RenderTexture trailTexture2;

    ComputeBuffer actorBuffer, speciesBuffer, settingsBuffer;

    Settings currentSettings;

    bool Initialized = false;

    // Start is called before the first frame update
    void Start()
    {
        currentSettings = new Settings( new Vector2[] {
            new Vector2(sensorSizeMin, sensorSizeMax),
            new Vector2(sensorAngleMin, sensorAngleMax),
            new Vector2(sensorDistanceMin, sensorDistanceMax),
            new Vector2(moveSpeedMin, moveSpeedMax),
            new Vector2(turnSpeedMin, turnSpeedMax)
        });

        defaultSpecies.index = 0;
        defaultSpecies.sensorAngle = 1;
        defaultSpecies.sensorDistance = 1;
        defaultSpecies.sensorSize = 1;
        defaultSpecies.turnSpeed = 1;
        defaultSpecies.moveSpeed = 1;
        defaultSpecies.color = new Vector4(1, 1, 1, 1);
        defaultSpecies.inverseColor = new Vector4(0, 0, 0, 0);

        trailTexture = new RenderTexture(resolution, resolution, 0);
        trailTexture.enableRandomWrite = true;
        trailTexture.filterMode = FilterMode.Point;
        trailTexture.wrapMode = TextureWrapMode.Mirror;

        drawTexture = new RenderTexture(trailTexture);
        trailTexture2 = new RenderTexture(trailTexture);

        createActorsCircle();
        actorBuffer = new ComputeBuffer(numActors, sizeof(int) + 3 * sizeof(float));
        actorBuffer.SetData(actors);
        speciesBuffer = new ComputeBuffer(numSpecies, sizeof(int) * 2 + sizeof(float) * 12);
        species = createSpecies();
        speciesBuffer.SetData(species);
        settingsBuffer = new ComputeBuffer(1, sizeof(float) * 10);
        settingsBuffer.SetData(new Settings[] { currentSettings });
        


        computeShader.SetTexture(0, "_trailTexToWrite", trailTexture);
        computeShader.SetTexture(0, "_trailTexToWrite2", trailTexture2);

        computeShader.SetTexture(1, "_trailTexToWrite", trailTexture);
        computeShader.SetTexture(1, "_trailTexToSample", trailTexture2);

        computeShader.SetTexture(2, "_trailTexToWrite", trailTexture2);
        computeShader.SetTexture(2, "_trailTexToSample", trailTexture);

        computeShader.SetTexture(3, "_trailTexToWrite", trailTexture);
        computeShader.SetTexture(3, "_trailTexToWrite2", trailTexture2);

        computeShader.SetInt("resolution", resolution);
        computeShader.SetBuffer(0, "actors", actorBuffer);
        computeShader.SetBuffer(0, "species", speciesBuffer);
        computeShader.SetBuffer(0, "settingsBuffer", settingsBuffer);

        computeShader.SetFloat("evaporateSpeed", evaporateSpeed);

        Renderer r = GetComponent<Renderer>();
        r.material.SetTexture("_MainTex", trailTexture2);
        r.material.SetTexture("_MetallicGlossMap", trailTexture2);
        r.material.SetTexture("_SmoothnessTextureChannel", trailTexture2);

        Initialized = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (Initialized)
        {
            computeShader.SetFloat("deltaTime", Time.deltaTime);
            computeShader.SetFloat("time", Time.time);
            computeShader.Dispatch(0, numActors / 16, 1, 1);

            computeShader.Dispatch(1, resolution / 8, resolution / 8, 1);
            computeShader.Dispatch(2, resolution / 8, resolution / 8, 1);
        }
    }

    private void OnValidate()
    {
        if(Initialized && UnityEditor.EditorApplication.isPlaying)
        {
            UpdateSettings();
        }
    }

    void UpdateSettings()
    {
        currentSettings = new Settings(new Vector2[] {
            new Vector2(sensorSizeMin, sensorSizeMax),
            new Vector2(sensorAngleMin, sensorAngleMax),
            new Vector2(sensorDistanceMin, sensorDistanceMax),
            new Vector2(moveSpeedMin, moveSpeedMax),
            new Vector2(turnSpeedMin, turnSpeedMax)
        });

        settingsBuffer.SetData(new Settings[] { currentSettings });
    }

    void remapFloatRange(float prevMin, float prevMax, float newMin, float newMax, ref float valueToRemap)
    {
        float prevRange = prevMax - prevMin;
        float newRange = newMax - newMin;

        float value = valueToRemap - prevMin;
        value = value / prevRange;
        value = value * newRange;
        value += newMin;

        valueToRemap = value;
    }

    void createActors()
    {
        actors = new Actor[numActors];
        for(int i = 0; i < numActors; i++)
        {
            Actor a = new Actor();
            a.species = i % numSpecies;
            a.position = new Vector2(
                UnityEngine.Random.value * resolution,
                UnityEngine.Random.value * resolution);
            a.angle = UnityEngine.Random.Range(0, 2 * Mathf.PI);
            actors[i] = a;
        }
    }

    void createActorsCircle()
    {
        actors = new Actor[numActors];
        for (int i = 0; i < numActors; i++)
        {
            Actor a = new Actor();
            a.species = i % 10;
            a.position = ((UnityEngine.Random.insideUnitCircle / 3) + Vector2.one / 2) * resolution;
            a.angle = UnityEngine.Random.Range(0, 2 * Mathf.PI);
            actors[i] = a;
        }
    }



    SpeciesBufferValues[] createSpecies()
    {
        SpeciesBufferValues[] createdSpecies = new SpeciesBufferValues[numSpecies];
        for(int i = 0; i < numSpecies; i++)
        {
            SpeciesBufferValues s = defaultSpecies.CloneViaSerialization<SpeciesBufferValues>();
            s.index = i;

            s.sensorSize = UnityEngine.Random.value;
            s.sensorAngle = UnityEngine.Random.value;
            s.sensorDistance = UnityEngine.Random.value;
            s.moveSpeed = UnityEngine.Random.value;
            s.turnSpeed = UnityEngine.Random.value;

            Color color = Color.HSVToRGB((float)i / numSpecies, 1, 1);

            Color inverseColor = Vector4.one - (Vector4)color;

            s.color = color;
            s.inverseColor = inverseColor;

            createdSpecies[i] = s;
        }

        return createdSpecies;
    }
}

public class Species
{
    public int index;
    public int sensorSize;
    public float sensorAngle;
    public float sensorDistance;
    public float turnSpeed;
    public float moveSpeed;
    public Vector4 color;
    public Vector4 inverseColor;

    private float[] a;

    public SpeciesBufferValues Values
    {
        get
        {
            return new SpeciesBufferValues(
                index,
                sensorSize,
                sensorAngle,
                sensorDistance,
                turnSpeed,
                moveSpeed,
                color,
                inverseColor);
        }
    }

    public void RemapRanges(float[,] ranges)
    {
        float[] values = new float[]
        {
            sensorAngle,
            sensorDistance,
            turnSpeed,
            moveSpeed
        };


    }

}

public struct Actor
{
    public int species;
    public Vector2 position;
    public float angle;
}

public struct SpeciesBufferValues
{

    public SpeciesBufferValues(
        int _index,
        float _sensorSize,
        float _sensorAngle,
        float _sensorDistance,
        float _moveSpeed,
        float _turnSpeed,
        Vector4 _color,
        Vector4 _inverseColor)
    {
        index = _index;
        sensorSize = _sensorSize;
        sensorAngle = _sensorAngle;
        sensorDistance = _sensorDistance;
        moveSpeed = _moveSpeed;
        turnSpeed = _turnSpeed;
        color = _color;
        inverseColor = _inverseColor;
    }

    public int index;
    public float sensorSize;
    public float sensorAngle;
    public float sensorDistance;
    public float moveSpeed;
    public float turnSpeed;
    public Vector4 color;
    public Vector4 inverseColor;
}

public struct SettingsBuilder
{
    public SettingsBuilder(
        float _sensorSizeMin,
        float _sensorSizeMax,
        float _sensorAngleMin,
        float _sensorAngleMax,
        float _sensorDistanceMin,
        float _sensorDistanceMax,
        float _moveSpeedMin,
        float _moveSpeedMax,
        float _turnSpeedMin,
        float _turnSpeedMax)
    {
        sensorSizeMin= _sensorSizeMin;
        sensorSizeMax= _sensorSizeMax;
        sensorAngleMin= _sensorAngleMin;
        sensorAngleMax= _sensorAngleMax;
        sensorDistanceMin= _sensorDistanceMin;
        sensorDistanceMax= _sensorDistanceMax;
        moveSpeedMin= _moveSpeedMin;
        moveSpeedMax= _moveSpeedMax;
        turnSpeedMin= _turnSpeedMin;
        turnSpeedMax= _turnSpeedMax;
    }

    public float sensorSizeMin, sensorSizeMax, 
        sensorAngleMin, sensorAngleMax, 
        sensorDistanceMin, sensorDistanceMax, 
        moveSpeedMin, moveSpeedMax, 
        turnSpeedMin, turnSpeedMax;
}

public struct Settings
{
    public Settings(Vector2[] ranges)
    {
        sensorSizeRange = ranges[0];
        sensorAngleRange = ranges[1];
        sensorDistanceRange = ranges[2];
        moveSpeedRange = ranges[3];
        turnSpeedRange = ranges[4];
    }
    public Vector2 sensorSizeRange;
    public Vector2 sensorAngleRange;
    public Vector2 sensorDistanceRange;
    public Vector2 moveSpeedRange;
    public Vector2 turnSpeedRange;
}
