using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;

[TestFixture]
public class NeigbourhoodTests

{
    private ComputeShader computeShader;

    private int kernelHandle;

    ComputeBuffer adjacentCellsBuffer = new ComputeBuffer(27, sizeof(int));

    // Parámetros de simulación
    private const float CubeSize = 8.0f;
    private const int NumCells = 4;

    [SetUp]
    public void Setup()
    {
        // Cargar el compute shader (asegúrate de que esté en tu proyecto con el nombre correcto)
        computeShader = Resources.Load<ComputeShader>("AuxMethods"); // Ajusta el nombre/ruta
        kernelHandle = computeShader.FindKernel("TestGetNeighbours");
        computeShader.SetBuffer(kernelHandle, "AdjacentCells", adjacentCellsBuffer);
    }

    [TearDown]
    public void Teardown()
    {
        // Liberar buffers
        adjacentCellsBuffer?.Release();
    }


    [UnityTest]
    public IEnumerator TestNeoghbours()
    {

        computeShader.SetInt("index", 124);
        computeShader.SetInt("side", 5);

        // Llamar al kernel de prueba
        computeShader.Dispatch(kernelHandle, 1, 1, 1);

        // Leer los datos del buffer
        int[] adjacentCellsData = new int[27];
        adjacentCellsBuffer.GetData(adjacentCellsData);

        for (int i = 0; i < adjacentCellsData.Length; i++)

        {

            int z = adjacentCellsData[i] / (5 * 5);
            int y = (adjacentCellsData[i] % (5 * 5)) / 5;
            int x = adjacentCellsData[i] % 5;


            Debug.Log(adjacentCellsData[i] + " " + x + " " + y + " " + z);
        }
        yield return null;
    }

}