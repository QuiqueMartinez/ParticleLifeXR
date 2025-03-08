using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

[TestFixture]
public class HashingTests
{
    private ComputeShader computeShader;
    private ComputeBuffer positionsBuffer;
    private ComputeBuffer hashesBuffer;
    private ComputeBuffer stackBuffer;
    private int kernelHandle;

    // Simulation parameters
    private const float CubeSize = 8.0f;
    private const int NumCells = 4;

    [SetUp]
    public void Setup()
    {
        computeShader = Resources.Load<ComputeShader>("BasicParticleCS");
        kernelHandle = computeShader.FindKernel("Hashing");
    }

    [TearDown]
    public void Teardown()
    {
        positionsBuffer?.Release();
        hashesBuffer?.Release();
        stackBuffer?.Release();
    }


    [UnityTest]
    public IEnumerator TestOneParticle()
    {
        Vector3[] positions = new Vector3[]
        {
            new Vector3(0.5f, 0.5f, 0.5f),
        };

        int[] expectedStack = new int[NumCells * NumCells * NumCells * 3];
        expectedStack[1] = positions.Length;
        expectedStack[2] = positions.Length;

        yield return TestPositionBatch(positions, expectedStack);
        }


    [UnityTest]
    public IEnumerator TestAllParticlesSameCell()
    {
        Vector3[] positions = new Vector3[]
        {
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f)
        };

        int[] expectedStack = new int[NumCells * NumCells * NumCells * 3];
        expectedStack[1] = positions.Length;
        expectedStack[2] = positions.Length;
        yield return TestPositionBatch(positions, expectedStack);
    }


    [UnityTest]
    public IEnumerator TestAllParticlesDifferentCells()
    {
        Vector3[] positions = new Vector3[]
        {
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(2.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 2.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 2.5f)
        };

        int[] expectedStack = new int[NumCells * NumCells * NumCells * 3];
        expectedStack[1] = 1;
        expectedStack[2] = 1;

        expectedStack[1 * 3 + 1] = 1;
        expectedStack[1 * 3 + 2] = 1;

        expectedStack[4 * 3 + 1] = 1;
        expectedStack[4 * 3 + 2] = 1;


        expectedStack[16 * 3 + 1] = 1;
        expectedStack[16 * 3 + 2] = 1;

        yield return TestPositionBatch(positions, expectedStack);
    }


    public IEnumerator TestPositionBatch(Vector3[] positions, int[] expectedStack)
    {

        int NumParticles = positions.Length;

        positionsBuffer = new ComputeBuffer(NumParticles, sizeof(float) * 3); 
        hashesBuffer = new ComputeBuffer(NumParticles, sizeof(int)); 
        stackBuffer = new ComputeBuffer(NumCells * NumCells * NumCells, sizeof(int) * 3); 

        Assert.AreEqual(expectedStack.Length / 3, stackBuffer.count , "Expected stack size is incorrect");

        // Cunfigure simulation parameters
        computeShader.SetFloat("cubeSize", CubeSize);
        computeShader.SetInt("numCells", NumCells);
        computeShader.SetInt("numParticles", NumParticles);

        computeShader.SetBuffer(kernelHandle, "positions", positionsBuffer);
        computeShader.SetBuffer(kernelHandle, "hashes", hashesBuffer);
        computeShader.SetBuffer(kernelHandle, "stack", stackBuffer);

        positionsBuffer.SetData(positions);

        int[] stackData = new int[NumCells * NumCells * NumCells * 3];
        stackBuffer.SetData(stackData);

        // Run kernel
        computeShader.Dispatch(kernelHandle, Mathf.CeilToInt(NumParticles / 1024f), 1, 1);

        // Results
        int[] hashes = new int[NumParticles];
        hashesBuffer.GetData(hashes);

        int[] stack = new int[NumCells * NumCells * NumCells * 3];
        stackBuffer.GetData(stack);

        float cellSize = CubeSize / NumCells;
        for (int i = 0; i < NumParticles; i++)
        {
            Vector3 pos = positions[i];
            int3 cellIndex = new int3(
                Mathf.FloorToInt((pos.x + CubeSize) / cellSize) % NumCells,
                Mathf.FloorToInt((pos.y + CubeSize) / cellSize) % NumCells,
                Mathf.FloorToInt((pos.z + CubeSize) / cellSize) % NumCells
            );
            int expectedHash = cellIndex.x + NumCells * (cellIndex.y + NumCells * cellIndex.z);

            Assert.AreEqual(expectedHash, hashes[i], $"Incorrect hash for particle {i}");
        }


        for (int i = 0; i < stack.Length; i++)
        {
            Assert.IsTrue(stack[i] == expectedStack[i], $"Invalid index for particle{i} : {stack[i]}");
        }

        CountAllParticles(stack, NumParticles);

        yield return null;
    }

    private struct int3
    {
        public int x, y, z;
        public int3(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
    }

    public void CountAllParticles(int[] stack, int NumParticles)
    {
        int counter_y = 0;
        int counter_z = 0;
        for (int i = 0; i < stack.Length; i++)
        {
            if (i % 3 == 1)
            {
                counter_y += stack[i];
            }
            if (i % 3 == 2)
            {
                counter_z += stack[i];
            }
        }
        Assert.IsTrue(counter_y == NumParticles, $"Total particulas en el stack y ({counter_y}) invalido");
        Assert.IsTrue(counter_z == NumParticles, $"Total particulas en el stack z ({counter_z}) invalido");
    }
}