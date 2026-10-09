using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Al "Cube Spawn Manager": genera GunCube / SwordCube aleatoris en els SpawnPoints amb dificultat progressiva.
public class CubeSpawner : MonoBehaviour
{
    [Header("Cubs")]
    [SerializeField] private GameObject[] cubePrefabs;
    [Tooltip("Si es deixa buit, agafa els fills que es diuen SpawnPoint...")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Ritme")]
    [SerializeField] private float startDelay = 2f;
    [SerializeField] private float initialInterval = 2f;
    [SerializeField] private float minInterval = 0.5f;
    [Tooltip("Quant es redueix l'interval cada cop que puja el nivell")]
    [SerializeField] private float intervalDecreasePerLevel = 0.15f;

    [Header("Dificultat")]
    [Tooltip("Segons que han de passar per pujar un nivell")]
    [SerializeField] private float secondsPerLevel = 10f;
    [SerializeField] private int maxLevel = 10;

    public int CurrentLevel { get; private set; } = 1;

    private Coroutine spawnRoutine;
    private int lastSpawnIndex = -1;

    void Awake()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            var found = new List<Transform>();
            foreach (Transform child in transform)
                if (child.name.StartsWith("SpawnPoint"))
                    found.Add(child);
            spawnPoints = found.ToArray();
        }
    }

    void Start()
    {
        StartSpawning();
    }

    public void StartSpawning()
    {
        if (spawnRoutine == null)
            spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        if (cubePrefabs == null || cubePrefabs.Length == 0 || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"{name}: falten prefabs o spawn points al CubeSpawner.", this);
            yield break;
        }

        yield return new WaitForSeconds(startDelay);

        float elapsed = 0f;
        while (true)
        {
            CurrentLevel = Mathf.Min(maxLevel, 1 + Mathf.FloorToInt(elapsed / secondsPerLevel));
            SpawnCube();

            float interval = Mathf.Max(minInterval, initialInterval - (CurrentLevel - 1) * intervalDecreasePerLevel);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }

    private void SpawnCube()
    {
        var prefab = cubePrefabs[Random.Range(0, cubePrefabs.Length)];

        // Evita repetir el mateix spawn point dues vegades seguides
        int index = Random.Range(0, spawnPoints.Length);
        if (spawnPoints.Length > 1 && index == lastSpawnIndex)
            index = (index + 1) % spawnPoints.Length;
        lastSpawnIndex = index;

        var point = spawnPoints[index];
        var cube = Instantiate(prefab, point.position, point.rotation);

        var physics = cube.GetComponent<CubePhysics>();
        if (physics != null)
            physics.movementLevel = CurrentLevel;
    }
}
