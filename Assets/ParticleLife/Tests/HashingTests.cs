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

    // Parámetros de simulación
    private const float CubeSize = 8.0f;
    private const int NumCells = 4;

    [SetUp]
    public void Setup()
    {
        // Cargar el compute shader (asegúrate de que esté en tu proyecto con el nombre correcto)
        computeShader = Resources.Load<ComputeShader>("BasicParticleCS"); // Ajusta el nombre/ruta
        kernelHandle = computeShader.FindKernel("Hashing");
    }

    [TearDown]
    public void Teardown()
    {
        // Liberar buffers
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
        // Preparar datos de prueba (posiciones de partículas)

        int NumParticles = positions.Length;

        positionsBuffer = new ComputeBuffer(NumParticles, sizeof(float) * 3); // float3
        hashesBuffer = new ComputeBuffer(NumParticles, sizeof(int)); // int
                                                                     // Inicializar buffers

        stackBuffer = new ComputeBuffer(NumCells * NumCells * NumCells, sizeof(int) * 3); // int3

        Assert.AreEqual(expectedStack.Length / 3, stackBuffer.count , "Expected stack size is incorrect");

        // Configurar parámetros de simulación en el cbuffer
        computeShader.SetFloat("cubeSize", CubeSize);
        computeShader.SetInt("numCells", NumCells);
        computeShader.SetInt("numParticles", NumParticles);

        // Vincular buffers al compute shader
        computeShader.SetBuffer(kernelHandle, "positions", positionsBuffer);
        computeShader.SetBuffer(kernelHandle, "hashes", hashesBuffer);
        computeShader.SetBuffer(kernelHandle, "stack", stackBuffer);

        // Subir datos al buffer
        positionsBuffer.SetData(positions);

        // Inicializar stack con ceros
        int[] stackData = new int[NumCells * NumCells * NumCells * 3];
        stackBuffer.SetData(stackData);

        // Ejecutar el compute shader
        computeShader.Dispatch(kernelHandle, Mathf.CeilToInt(NumParticles / 1024f), 1, 1);

        // Obtener resultados
        int[] hashes = new int[NumParticles];
        hashesBuffer.GetData(hashes);

        int[] stack = new int[NumCells * NumCells * NumCells * 3];
        stackBuffer.GetData(stack);

        // Verificar resultados
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

            Assert.AreEqual(expectedHash, hashes[i], $"Hash incorrecto para la partícula {i}");
        }


        for (int i = 0; i < stack.Length; i++)
        {
            Assert.IsTrue(stack[i] == expectedStack[i], $"Contador inválido en la celda {i} : {stack[i]}");
        }

        CountAllParticles(stack, NumParticles);

        yield return null;
    }

    // Estructura auxiliar para replicar int3
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