using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class ParticleLifeController : MonoBehaviour
{
    bool paused = true;

    public VisualEffect vfxGraph;
    public ComputeShader interactionShader;

    // Intermediate buffers
    private GraphicsBuffer positionBuffer;
    private GraphicsBuffer velocityBuffer;
    private GraphicsBuffer deltasBuffer;
    private ComputeBuffer hashingBuffer;
    private ComputeBuffer sortedBuffer;
    private ComputeBuffer stackBuffer; 

    // Simulation buffers
    private ComputeBuffer interactionMatrixBuffer;
    private ComputeBuffer debugResultsBuffer;

    private const int particleCount = 30000;
    private const int particleTypes = 5;
    private const float cubesize = 20.0f;
    
    // Spatial hashing parameters
    private const int cubeSideCells = 10;
    private const int totalCells = cubeSideCells * cubeSideCells * cubeSideCells;

    private int HashingKernelIndex;
    private int SortingKernelIndex;
    private int InteractionKernelIndex;
    private int ApplyDeltasKernelIndex;


    [Range(0.90f, 1.0f)]
    [SerializeField] float friction = 0.98f;

    [Range(0.0f, 1.0f)]
    [SerializeField] float globalDistanceAttenuation = 0.85f;


    [Range(0.0f, 10.0f)]
    [SerializeField] float globalForceGain = 0.5f;

    [Range(0.0f, 1.0f)]
    [SerializeField] float repulsiveForceAttenuation = 0.5f;

    [Range(0.0f, 1.0f)]
    [SerializeField] float particleForceAttenuation = 0.5f;



    [Range(0.0f, 10.0f)]
    [SerializeField] float fastSigmoidAttenuation = 0.5f;

    [Range(0.0f, 1.0f)]
    [SerializeField] float interactionCenter = 0.5f;

    [Range(0.0f, 0.5f)]
    [SerializeField] float interactionCenterWidth = 0.1f;

    [Range(0.0f, 0.5f)]
    [SerializeField] float interactionForceWidth = 0.3f;

    public struct InteractionParameters
    {
        public float minDistance;
        public float radius;
        public float maxDistance;

        public float originForce; // Repulsive force at origin
        public float radiusForce; // Force at radius (attractive or repulsive)
    }

    InteractionParameters[] interactionMatrix = new InteractionParameters[particleTypes * particleTypes];

    private void Awake()
    {
        HashingKernelIndex = interactionShader.FindKernel("Hashing");
        SortingKernelIndex = interactionShader.FindKernel("Sorting");
        InteractionKernelIndex = interactionShader.FindKernel("CalculateInteractions");
        ApplyDeltasKernelIndex = interactionShader.FindKernel("ApplyDeltas");
    }

    void Start()
    {
        Cursor.visible = false;

        positionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, sizeof(float) * 3);
        velocityBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, sizeof(float) * 3);
        deltasBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, sizeof(float) * 3);

        hashingBuffer = new ComputeBuffer(particleCount, sizeof(int));
        sortedBuffer = new ComputeBuffer(particleCount, sizeof(int));
        stackBuffer = new ComputeBuffer(totalCells, sizeof(int) * 3);

        interactionMatrixBuffer = new ComputeBuffer(interactionMatrix.Length, sizeof(float) * 5);
        debugResultsBuffer = new ComputeBuffer(3, sizeof(int));

        Vector3[] initialPositions = new Vector3[particleCount];
        Vector3[] initialVelocities = new Vector3[particleCount];

        // Distibute particles in a sphere
        for (int i = 0; i < particleCount; i++)
        {
            initialPositions[i] = (UnityEngine.Random.insideUnitSphere) *10f + Vector3.one * 10;
            initialVelocities[i] = 0.001f*UnityEngine.Random.onUnitSphere * 1f;
        }

        positionBuffer.SetData(initialPositions);
        velocityBuffer.SetData(initialVelocities);

        // Set buffers
        interactionShader.SetBuffer(HashingKernelIndex, "positions", positionBuffer);
        interactionShader.SetBuffer(HashingKernelIndex, "hashes", hashingBuffer);
        interactionShader.SetBuffer(HashingKernelIndex, "stack", stackBuffer);

        interactionShader.SetBuffer(SortingKernelIndex, "hashes", hashingBuffer);
        interactionShader.SetBuffer(SortingKernelIndex, "sorted", sortedBuffer);
        interactionShader.SetBuffer(SortingKernelIndex, "stack", stackBuffer);

        interactionShader.SetBuffer(InteractionKernelIndex, "positions", positionBuffer);
        interactionShader.SetBuffer(InteractionKernelIndex, "sorted", sortedBuffer);
        interactionShader.SetBuffer(InteractionKernelIndex, "stack", stackBuffer);
        interactionShader.SetBuffer(InteractionKernelIndex, "hashes", hashingBuffer);
        interactionShader.SetBuffer(InteractionKernelIndex, "deltas", deltasBuffer);
        interactionShader.SetBuffer(InteractionKernelIndex, "interactionMatrix", interactionMatrixBuffer);
        interactionShader.SetBuffer(InteractionKernelIndex, "debugResults", debugResultsBuffer);

        interactionShader.SetBuffer(ApplyDeltasKernelIndex, "positions", positionBuffer);
        interactionShader.SetBuffer(ApplyDeltasKernelIndex, "velocities", velocityBuffer);
        interactionShader.SetBuffer(ApplyDeltasKernelIndex, "deltas", deltasBuffer);


        interactionShader.SetFloat("cubeSize", cubesize);
        interactionShader.SetInt("numCells", cubeSideCells);
        interactionShader.SetInt("numParticles", particleCount);

        InitializeInteractionMatrix();

        vfxGraph.SetGraphicsBuffer(Shader.PropertyToID("positions"), positionBuffer);
   }

    void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            Vector3[] initialVelocities = new Vector3[particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                initialVelocities[i] = 0.001f * UnityEngine.Random.onUnitSphere * 1f;
            }
            velocityBuffer.SetData(initialVelocities);

            InitializeInteractionMatrix();
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            paused = !paused;
        }

        if (paused)
        {
            return;
        }

        interactionShader.SetFloat("deltaTime", Time.deltaTime);
        int[] initialDebugData = new int[3] { 0, 0, 0}; // touchedCells = 0, evaluatedInteractions = 0
        debugResultsBuffer.SetData(initialDebugData);

        // Clear stack buffer that keeps the particle allocation per cell
        int3[] stackInit = new int3[totalCells];
        for (int i = 0; i < totalCells; i++) stackInit[i] = new int3(0, 0, 0);
        stackBuffer.SetData(stackInit);

        // Dispatch kernels
        interactionShader.Dispatch(HashingKernelIndex, particleCount / 1024, 1, 1);

        int[] datain = new int[totalCells * 3];
        stackBuffer.GetData(datain);
        SetCellRanges(ref datain);
        stackBuffer.SetData(datain);

        interactionShader.Dispatch(SortingKernelIndex, (particleCount / 1024) + 1, 1, 1);
        interactionShader.Dispatch(InteractionKernelIndex, (particleCount / 1024) + 1, 1, 1);
        interactionShader.Dispatch(ApplyDeltasKernelIndex, (particleCount / 1024) + 1, 1, 1);

        int[] results = new int[3];
        debugResultsBuffer.GetData(results);
        Debug.Log("Touched particles: " + results[0]);
        Debug.Log("Evaluated Interactions: " + results[1]);
        Debug.Log("Evaluated Cells: " + results[2]);

    }

    public static void SetCellRanges(ref int[] data)
    {
        int accumulator = 0;
        for (int i = 0; i < data.Length; i = i + 3)
        {
            data[i] = accumulator;
            accumulator += data[i + 1];
        }
    }

    void InitializeInteractionMatrix()
    {
        interactionShader.SetFloat("friction", friction);
        interactionShader.SetFloat("fastSigmoidAttenuation", fastSigmoidAttenuation);

        for (int i = 0; i < particleTypes; i++)
        {
            for (int j = 0; j < particleTypes  ; j++)
            {
                int index = i * particleTypes + j;

                float centerlow = Mathf.Clamp01(interactionCenter - interactionCenterWidth);
                float centerHigh = Mathf.Clamp01(interactionCenter + interactionCenterWidth);
                float center = UnityEngine.Random.Range(centerlow, centerHigh);


                interactionMatrix[index].minDistance = Mathf.Clamp01( globalDistanceAttenuation 
                    *(center - interactionForceWidth));
                interactionMatrix[index].radius = globalDistanceAttenuation * center;
                interactionMatrix[index].maxDistance = Mathf.Clamp01(globalDistanceAttenuation
                    * center + interactionForceWidth);

                interactionMatrix[index].originForce =  - globalForceGain * repulsiveForceAttenuation *  UnityEngine.Random.Range(1,10);
                interactionMatrix[index].radiusForce =  -globalForceGain * particleForceAttenuation * UnityEngine.Random.Range(-1,1);

            }
        }
        interactionMatrixBuffer.SetData(interactionMatrix);
    }

    private void OnDestroy()
    {
        positionBuffer.Release();
        velocityBuffer.Release();
        deltasBuffer.Release();
        hashingBuffer.Release();
        sortedBuffer.Release();
        stackBuffer.Release();
    }
}