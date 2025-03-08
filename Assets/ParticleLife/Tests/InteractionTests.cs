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

    // Parámetros de simulación
    private const float CubeSize = 10.0f;
    private const int NumCells = 5; //5x5x5

    [SetUp]
    public void Setup()
    {
        // Cargar el compute shader (asegúrate de que esté en tu proyecto con el nombre correcto)
        computeShader = Resources.Load<ComputeShader>("BasicParticleCS"); // Ajusta el nombre/ruta
        kernelHandle = computeShader.FindKernel("CalculateInteractions");
    }

    [TearDown]
    public void Teardown()
    {
        // Liberar buffers
        positionsBuffer?.Release();
        hashesBuffer?.Release();
        stackBuffer?.Release();
    }

    Vector3[] positions;

    public IEnumerator TestOneParticle()
    {

        positions = new Vector3[]
    {
                new Vector3(5.0f, 5.0f, 5.0f), // Middle Cell

    };

        yield return TestInteractionnBatch( 1,     0);
    }

    [UnityTest]
    public IEnumerator TestAccumulator()
    {
        int[] data = new int[]
    {
            0, 1, 1,
            0, 4, 4, // 1
            0, 0, 0, // 5
            0, 3, 3, // 5 
            0, 1, 1, // 8
            0, 1, 1  //9
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


    //[UnityTest]
    public IEnumerator TestInteractionnBatch(int particleIndex,  float expectedDelta)
    {
        // Preparar datos de prueba (posiciones de partículas)

        int NumParticles = positions.Length;

        positionsBuffer = new ComputeBuffer(NumParticles, sizeof(float) * 3); // float3

        stackBuffer = new ComputeBuffer(NumCells * NumCells * NumCells, sizeof(int) * 3); // int3

        //Assert.AreEqual(expectedStack.Length / 3, stackBuffer.count, "Expected stack size is incorrect");

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
           /* int3 cellIndex = new int3(
                Mathf.FloorToInt((pos.x + CubeSize) / cellSize) % NumCells,
                Mathf.FloorToInt((pos.y + CubeSize) / cellSize) % NumCells,
                Mathf.FloorToInt((pos.z + CubeSize) / cellSize) % NumCells
            );
            int expectedHash = cellIndex.x + NumCells * (cellIndex.y + NumCells * cellIndex.z);
           */
            //Assert.AreEqual(expectedHash, hashes[i], $"Hash incorrecto para la partícula {i}");
        }

        /*
        for (int i = 0; i < stack.Length; i++)
        {
            Assert.IsTrue(stack[i] == expectedStack[i], $"Contador inválido en la celda {i} : {stack[i]}");
        }

        CountAllParticles(stack, NumParticles);*/

        yield return null;
    }
}
