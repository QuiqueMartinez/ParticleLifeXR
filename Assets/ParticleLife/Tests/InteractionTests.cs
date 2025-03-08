using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

[TestFixture]

public class InteractionTests 
{
    private ComputeShader computeShader;
    private ComputeBuffer positionsBuffer;
    private ComputeBuffer hashesBuffer;
    private ComputeBuffer stackBuffer;
    private int kernelHandle;


    private const float CubeSize = 10.0f;
    private const int NumCells = 5; 

    [SetUp]
    public void Setup()
    {
        computeShader = Resources.Load<ComputeShader>("BasicParticleCS"); 
        kernelHandle = computeShader.FindKernel("CalculateInteractions");
    }

    [TearDown]
    public void Teardown()
    {
        positionsBuffer?.Release();
        hashesBuffer?.Release();
        stackBuffer?.Release();
    }

    Vector3[] positions;

    public IEnumerator TestOneParticle()
    {

        positions = new Vector3[]
        {
                new Vector3(5.0f, 5.0f, 5.0f), 

        };
        yield return TestInteractionnBatch( 1,     0);
    }

    [UnityTest]
    public IEnumerator TestAccumulator()
    {
        int[] data = new int[]
    {
            0, 1, 1,
            0, 4, 4, 
            0, 0, 0, 
            0, 3, 3, 
            0, 1, 1, 
            0, 1, 1  
    };
        SetCellRanges(ref data);

        for (int i = 0; i < data.Length / 3 ; i++)
        {
            Debug.Log(data[3 * i ] + " " + data[3 * i + 1]);
        }
        yield return null;
    }


    public static void SetCellRanges(ref int[] data)
    {
       int accumulator = 0;
       for (int i = 0; i < data.Length ; i = i + 3)
        {
             Debug.Log("i " + i/3 + " x " + data[i ] + " y " + data[i  + 1]);
            data[i ] = accumulator;
            accumulator += data[i  + 1];
        }

    }


    //[UnityTest] // Not used
    public IEnumerator TestInteractionnBatch(int particleIndex,  float expectedDelta)
    {
        int NumParticles = positions.Length;

        positionsBuffer = new ComputeBuffer(NumParticles, sizeof(float) * 3); // float3

        stackBuffer = new ComputeBuffer(NumCells * NumCells * NumCells, sizeof(int) * 3); // int3


        computeShader.SetFloat("cubeSize", CubeSize);
        computeShader.SetInt("numCells", NumCells);
        computeShader.SetInt("numParticles", NumParticles);


        computeShader.SetBuffer(kernelHandle, "positions", positionsBuffer);
        computeShader.SetBuffer(kernelHandle, "hashes", hashesBuffer);
        computeShader.SetBuffer(kernelHandle, "stack", stackBuffer);

        positionsBuffer.SetData(positions);
        int[] stackData = new int[NumCells * NumCells * NumCells * 3];
        stackBuffer.SetData(stackData);


        computeShader.Dispatch(kernelHandle, Mathf.CeilToInt(NumParticles / 1024f), 1, 1);


        int[] hashes = new int[NumParticles];
        hashesBuffer.GetData(hashes);

        int[] stack = new int[NumCells * NumCells * NumCells * 3];
        stackBuffer.GetData(stack);


        float cellSize = CubeSize / NumCells;
        for (int i = 0; i < NumParticles; i++)
        {
            Vector3 pos = positions[i];
        }

        yield return null;
    }
}
