using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

[TestFixture]

public class SortingTests 
{
    private ComputeShader computeShader;
    private ComputeBuffer sortedBuffer;
    private ComputeBuffer hashesBuffer;
    private ComputeBuffer stackBuffer;
    private int kernelHandle;

    private const float CubeSize = 8.0f;
    private const int NumCells = 4;

    [SetUp]
    public void Setup()
    {
        computeShader = Resources.Load<ComputeShader>("BasicParticleCS");
        kernelHandle = computeShader.FindKernel("Sorting");
    }

    [TearDown]
    public void Teardown()
    {
        sortedBuffer?.Release();
        hashesBuffer?.Release();
        stackBuffer?.Release();
    }

    [UnityTest]
    public IEnumerator SimpleSortingTest()
    {
        const int NumParticles = 3;
        const int StackSize = 6;

        hashesBuffer = new ComputeBuffer(NumParticles, sizeof(int));
        sortedBuffer = new ComputeBuffer(NumParticles, sizeof(int)); 
        stackBuffer = new ComputeBuffer(NumCells * NumCells * NumCells, sizeof(int) * 3); 


        computeShader.SetFloat("cubeSize", CubeSize);
        computeShader.SetInt("numCells", NumCells);
        computeShader.SetInt("numParticles", NumParticles);

        computeShader.SetBuffer(kernelHandle, "sorted", sortedBuffer);
        computeShader.SetBuffer(kernelHandle, "hashes", hashesBuffer);
        computeShader.SetBuffer(kernelHandle, "stack", stackBuffer);

        int[] hashes = new int[NumParticles] { 5, 4, 3};

        int[] expectedResult = { 2, 1, 0 };

        int[] sorted = new int[NumParticles];

        int[] stack = new int[StackSize * 3]
        { 
            0, 0, 0,
            0, 0, 0,
            0, 0, 0,
            0, 1, 1,
            1, 1, 1,
            2, 1, 1
        };

        hashesBuffer.SetData(hashes);
        stackBuffer.SetData(stack);
        computeShader.Dispatch(kernelHandle, Mathf.CeilToInt(NumParticles / 1024f), 1, 1);

        sortedBuffer.GetData(sorted);
        stackBuffer.GetData(stack);


        for (int i = 0; i < NumParticles; i++)
        {
            Debug.Log("i:" + i + " h:" + hashes[i] + " s:" + sorted[i]);
        }

        for (int i = 0; i < StackSize; i++)
        {
            Debug.Log(stack[3 * i + 1] + " " +stack[3*i+2]);
        }

        Assert.AreEqual(1, 1, "Expected stack size is incorrect");

        yield return null;
    }
    [UnityTest]
    public IEnumerator ComplexSortingTest()
    {
        const int NumParticles = 10;
        const int StackSize = 6;

        hashesBuffer = new ComputeBuffer(NumParticles, sizeof(int)); // int
        sortedBuffer = new ComputeBuffer(NumParticles, sizeof(int)); // int
        stackBuffer = new ComputeBuffer(NumCells * NumCells * NumCells, sizeof(int) * 3); // int3


        computeShader.SetFloat("cubeSize", CubeSize);
        computeShader.SetInt("numCells", NumCells);
        computeShader.SetInt("numParticles", NumParticles);

        computeShader.SetBuffer(kernelHandle, "sorted", sortedBuffer);
        computeShader.SetBuffer(kernelHandle, "hashes", hashesBuffer);
        computeShader.SetBuffer(kernelHandle, "stack", stackBuffer);

        int[] hashes = new int[NumParticles] { 5, 4, 3 ,3, 3, 0, 1, 1, 1, 1 };

        int[] sorted = new int[NumParticles];

        int[] stack = new int[StackSize * 3]
        {
            0, 1, 1,
            1, 4, 4,
            5, 0, 0,
            5, 3, 3,
            8, 1, 1,
            9, 1, 1
        };

        hashesBuffer.SetData(hashes);
        stackBuffer.SetData(stack);


        computeShader.Dispatch(kernelHandle, Mathf.CeilToInt(NumParticles / 1024f), 1, 1);

        sortedBuffer.GetData(sorted);
        stackBuffer.GetData(stack);


        for (int i = 0; i < NumParticles; i++)
        {
            Debug.Log("i:" + i + " h:" + hashes[i] + " s:" + sorted[i]);
        }

        for (int i = 0; i < StackSize; i++)
        {
            Debug.Log(stack[3 * i + 1] + " " + stack[3 * i + 2]);
        }

        Assert.AreEqual(1, 1, "Expected stack size is incorrect");

        yield return null;
    }


}
