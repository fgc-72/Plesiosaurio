using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishSchool
{
    /// <summary>
    /// Cardumenes en GPU: simulacion boids en un compute shader (con rejilla espacial) y dibujo
    /// indirecto de todos los peces sin volver a la CPU. Un GameObject con este componente = una
    /// zona de simulacion (su posicion es el centro y "Bounds Size" su tamano).
    ///
    /// Los peces se apartan de los "Obstacles" (tiburones, el jugador...). Cada especie tiene su
    /// propia malla, material y comportamiento.
    /// </summary>
    public class FishSchoolManager : MonoBehaviour
    {
        const int MaxObstacles = 32;
        const int ThreadGroupSize = 64;

        // ------------------------------------------------------------------ Tipos
        [Serializable]
        public class Species
        {
            public string name = "Pez";
            public Mesh mesh;
            [Tooltip("Material con el shader Fish/FishSwim.")]
            public Material material;
            [Min(1)] public int count = 300;
            [Tooltip("Tamano relativo. Los peces pequenos huyen de los grandes; los grandes ignoran a los pequenos.")]
            [Min(0.01f)] public float size = 1f;

            [Header("Aparicion")]
            [Tooltip("Opcional. Si esta vacio aparecen en el centro de la zona.")]
            public Transform spawnPoint;
            [Min(0.1f)] public float spawnRadius = 8f;
            [Tooltip("Marcado: aparecen sobre la superficie de una esfera (util para bolas de peces). Desmarcado: dentro del volumen.")]
            public bool spawnOnSurface = false;

            [Header("Objetivo")]
            [Tooltip("Opcional. Si esta vacio el cardumen sigue un objetivo que vaga solo por la zona.")]
            public Transform target;
            [Range(0f, 3f)] public float targetWeight = 0.6f;
            [Tooltip("Velocidad con la que se mueve el objetivo automatico.")]
            [Min(0f)] public float wanderSpeed = 0.06f;

            [Header("Comportamiento (boids)")]
            [Range(0f, 4f)] public float cohesionWeight = 1f;
            [Range(0f, 4f)] public float alignmentWeight = 1f;
            [Range(0f, 6f)] public float separationWeight = 1.5f;
            [Min(0.1f)] public float perceptionRadius = 3f;
            [Min(0.05f)] public float separationRadius = 1f;

            [Header("Movimiento")]
            [Min(0.1f)] public float minSpeed = 1.5f;
            [Min(0.1f)] public float maxSpeed = 4f;
            [Min(0.1f)] public float maxAccel = 6f;
            [Tooltip("Velocidad maxima de giro en radianes por segundo.")]
            [Min(0.1f)] public float turnRate = 3f;
            [Tooltip("Parte de la aceleracion angular que se convierte en impulso al girar.")]
            [Range(0f, 1f)] public float turnBoost = 0.25f;
            [Range(0f, 20f)] public float avoidWeight = 8f;

            [Header("Nado")]
            [Range(0f, 1f)] public float idleSwim = 0.25f;
            [Min(0f)] public float swimFreqBase = 5f;
            [Min(0f)] public float swimFreqGain = 9f;
        }

        [Serializable]
        public class Obstacle
        {
            public Transform transform;
            [Min(0.1f)] public float radius = 3f;
        }

        // Estructuras GPU: deben coincidir con FishSchool.compute y FishSwim.shader.
        [StructLayout(LayoutKind.Sequential)]
        struct FishData
        {
            public Vector3 position;
            public float phase;
            public Vector3 velocity;
            public float intensity;
            public uint species;
            public float pad0, pad1, pad2;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SpeciesData
        {
            public Vector3 target;
            public float targetWeight;
            public float cohesionWeight, alignmentWeight, separationWeight, perceptionRadius;
            public float separationRadius, minSpeed, maxSpeed, maxAccel;
            public float turnRate, size, swimFreqBase, swimFreqGain;
            public float turnBoost, avoidWeight, idleSwim, pad;
        }

        // ------------------------------------------------------------------ Parametros
        [Header("Simulacion")]
        public ComputeShader compute;
        [Tooltip("Tamano de la zona. El centro es la posicion de este GameObject.")]
        public Vector3 boundsSize = new Vector3(60f, 25f, 60f);
        [Tooltip("Tamano de las celdas de la rejilla. Lo ideal es parecido al mayor 'Perception Radius'.")]
        [Min(0.5f)] public float cellSize = 3.5f;
        [Tooltip("Maximo de peces que se anotan por celda. Si una celda se llena, los sobrantes no se ven entre si.")]
        [Range(16, 256)] public int maxPerCell = 96;
        [Range(0f, 10f)] public float boundsWeight = 3f;
        [Tooltip("Distancia a la que los peces empiezan a esquivar un obstaculo (desde su borde).")]
        [Min(0.1f)] public float obstacleMargin = 4f;
        [Tooltip("Limite del paso de tiempo, para que un tiron no desordene la simulacion.")]
        [Range(0.01f, 0.1f)] public float maxDeltaTime = 0.05f;

        [Header("Especies")]
        public List<Species> species = new List<Species> { new Species() };

        [Header("Obstaculos")]
        [Tooltip("Tiburones, el jugador, rocas grandes... Los peces se apartan de ellos.")]
        public List<Obstacle> obstacles = new List<Obstacle>();

        [Header("Depuracion")]
        public bool drawGizmos = true;

        // ------------------------------------------------------------------ Estado
        static readonly int FishId = Shader.PropertyToID("_Fish");
        static readonly int SpeciesOffsetId = Shader.PropertyToID("_SpeciesOffset");

        ComputeBuffer fishA, fishB;
        ComputeBuffer speciesBuffer, obstacleBuffer, cellCount, cellFish;
        GraphicsBuffer[] argsBuffers;
        MaterialPropertyBlock[] blocks;

        SpeciesData[] speciesData;
        Vector4[] obstacleData;
        int[] speciesOffsets;
        int totalFish;
        int cellTotal;
        Vector3Int gridDim;

        int kClear, kFill, kSim;
        bool ready;

        ComputeBuffer ReadBuffer => fishA;   // despues de cada Simulate se intercambian
        ComputeBuffer WriteBuffer => fishB;

        // ------------------------------------------------------------------ Ciclo de vida
        void Reset()
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("FishSchool t:ComputeShader");
            if (guids.Length > 0)
                compute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#endif
        }

        void OnEnable()
        {
            ready = Initialize();
        }

        void OnDisable()
        {
            ReleaseAll();
            ready = false;
        }

        bool Initialize()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogError("[FishSchool] Esta plataforma no soporta compute shaders.");
                return false;
            }
            if (compute == null)
            {
                Debug.LogError("[FishSchool] Falta asignar el compute shader FishSchool.compute en el manager.");
                return false;
            }

            // Validar especies.
            totalFish = 0;
            foreach (Species s in species)
            {
                if (s.mesh == null || s.material == null)
                {
                    Debug.LogError("[FishSchool] La especie '" + s.name + "' necesita malla y material.");
                    return false;
                }
                totalFish += Mathf.Max(1, s.count);
            }
            if (species.Count == 0 || totalFish == 0) return false;

            // Rejilla espacial.
            gridDim = new Vector3Int(
                Mathf.Max(1, Mathf.CeilToInt(boundsSize.x / cellSize)),
                Mathf.Max(1, Mathf.CeilToInt(boundsSize.y / cellSize)),
                Mathf.Max(1, Mathf.CeilToInt(boundsSize.z / cellSize)));
            cellTotal = gridDim.x * gridDim.y * gridDim.z;
            if (cellTotal > 400000)
            {
                Debug.LogError("[FishSchool] La rejilla tiene demasiadas celdas (" + cellTotal + "). Sube 'Cell Size' o reduce 'Bounds Size'.");
                return false;
            }

            kClear = compute.FindKernel("ClearGrid");
            kFill = compute.FindKernel("FillGrid");
            kSim = compute.FindKernel("Simulate");

            int fishStride = Marshal.SizeOf<FishData>();
            fishA = new ComputeBuffer(totalFish, fishStride);
            fishB = new ComputeBuffer(totalFish, fishStride);
            speciesBuffer = new ComputeBuffer(species.Count, Marshal.SizeOf<SpeciesData>());
            obstacleBuffer = new ComputeBuffer(MaxObstacles, sizeof(float) * 4);
            cellCount = new ComputeBuffer(cellTotal, sizeof(uint));
            cellFish = new ComputeBuffer(cellTotal * maxPerCell, sizeof(uint));

            speciesData = new SpeciesData[species.Count];
            obstacleData = new Vector4[MaxObstacles];
            speciesOffsets = new int[species.Count];
            argsBuffers = new GraphicsBuffer[species.Count];
            blocks = new MaterialPropertyBlock[species.Count];

            // Datos iniciales de los peces.
            FishData[] initial = new FishData[totalFish];
            int index = 0;
            for (int s = 0; s < species.Count; s++)
            {
                Species sp = species[s];
                int count = Mathf.Max(1, sp.count);
                speciesOffsets[s] = index;

                Vector3 center = sp.spawnPoint != null ? sp.spawnPoint.position : transform.position;
                for (int k = 0; k < count; k++)
                {
                    // Puntos repartidos de forma pareja sobre una esfera (espiral de Fibonacci).
                    float y = 1f - 2f * (k + 0.5f) / count;
                    float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                    float theta = 2.39996323f * k;
                    Vector3 dir = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r);
                    float radiusFactor = sp.spawnOnSurface ? 1f : Mathf.Pow(UnityEngine.Random.value, 1f / 3f);

                    Vector3 pos = center + dir * sp.spawnRadius * radiusFactor;
                    pos = ClampToBounds(pos);

                    initial[index + k] = new FishData
                    {
                        position = pos,
                        phase = UnityEngine.Random.value * Mathf.PI * 2f,
                        velocity = UnityEngine.Random.onUnitSphere * Mathf.Lerp(sp.minSpeed, sp.maxSpeed, 0.4f),
                        intensity = sp.idleSwim,
                        species = (uint)s
                    };
                }
                index += count;

                // Comando de dibujo indirecto de esta especie.
                argsBuffers[s] = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
                var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
                args[0].indexCountPerInstance = sp.mesh.GetIndexCount(0);
                args[0].instanceCount = (uint)count;
                args[0].startIndex = sp.mesh.GetIndexStart(0);
                args[0].baseVertexIndex = sp.mesh.GetBaseVertex(0);
                args[0].startInstance = 0;
                argsBuffers[s].SetData(args);

                blocks[s] = new MaterialPropertyBlock();
            }

            fishA.SetData(initial);
            fishB.SetData(initial);
            return true;
        }

        void ReleaseAll()
        {
            Release(ref fishA);
            Release(ref fishB);
            Release(ref speciesBuffer);
            Release(ref obstacleBuffer);
            Release(ref cellCount);
            Release(ref cellFish);

            if (argsBuffers != null)
            {
                for (int i = 0; i < argsBuffers.Length; i++)
                {
                    argsBuffers[i]?.Release();
                    argsBuffers[i] = null;
                }
                argsBuffers = null;
            }
        }

        static void Release(ref ComputeBuffer b)
        {
            if (b != null)
            {
                b.Release();
                b = null;
            }
        }

        // ------------------------------------------------------------------ Frame
        void Update()
        {
            if (!ready) return;

            Simulate();
            Render();
        }

        void Simulate()
        {
            Vector3 center = transform.position;
            Vector3 extents = boundsSize * 0.5f;

            // Datos por especie (el objetivo se mueve, por eso se reenvian cada frame).
            for (int i = 0; i < species.Count; i++)
            {
                Species s = species[i];

                Vector3 target;
                float targetWeight;
                if (s.target != null)
                {
                    target = s.target.position;
                    targetWeight = s.targetWeight;
                }
                else
                {
                    target = WanderTarget(i, s, center, extents);
                    targetWeight = s.targetWeight;
                }

                speciesData[i] = new SpeciesData
                {
                    target = target,
                    targetWeight = targetWeight,
                    cohesionWeight = s.cohesionWeight,
                    alignmentWeight = s.alignmentWeight,
                    separationWeight = s.separationWeight,
                    perceptionRadius = s.perceptionRadius,
                    separationRadius = s.separationRadius,
                    minSpeed = s.minSpeed,
                    maxSpeed = Mathf.Max(s.maxSpeed, s.minSpeed),
                    maxAccel = s.maxAccel,
                    turnRate = s.turnRate,
                    size = s.size,
                    swimFreqBase = s.swimFreqBase,
                    swimFreqGain = s.swimFreqGain,
                    turnBoost = s.turnBoost,
                    avoidWeight = s.avoidWeight,
                    idleSwim = s.idleSwim,
                    pad = 0f
                };
            }
            speciesBuffer.SetData(speciesData);

            // Obstaculos.
            int obstacleCount = 0;
            for (int i = 0; i < obstacles.Count && obstacleCount < MaxObstacles; i++)
            {
                Obstacle o = obstacles[i];
                if (o == null || o.transform == null) continue;
                Vector3 p = o.transform.position;
                obstacleData[obstacleCount++] = new Vector4(p.x, p.y, p.z, o.radius);
            }
            obstacleBuffer.SetData(obstacleData);

            // Parametros globales del compute.
            Vector3 origin = center - extents;
            compute.SetInt("_FishCount", totalFish);
            compute.SetInt("_CellTotal", cellTotal);
            compute.SetInt("_MaxPerCell", maxPerCell);
            compute.SetInt("_ObstacleCount", obstacleCount);
            compute.SetInts("_GridDim", gridDim.x, gridDim.y, gridDim.z);
            compute.SetVector("_GridOrigin", origin);
            compute.SetFloat("_CellSize", cellSize);
            compute.SetFloat("_DeltaTime", Mathf.Min(Time.deltaTime, maxDeltaTime));
            compute.SetVector("_BoundsCenter", center);
            compute.SetVector("_BoundsExtents", extents);
            compute.SetFloat("_BoundsWeight", boundsWeight);
            compute.SetFloat("_ObstacleMargin", obstacleMargin);

            // 1) limpiar rejilla
            compute.SetBuffer(kClear, "_CellCount", cellCount);
            compute.Dispatch(kClear, Mathf.CeilToInt(cellTotal / (float)ThreadGroupSize), 1, 1);

            // 2) anotar cada pez en su celda
            compute.SetBuffer(kFill, "_FishRead", ReadBuffer);
            compute.SetBuffer(kFill, "_CellCount", cellCount);
            compute.SetBuffer(kFill, "_CellFish", cellFish);
            compute.Dispatch(kFill, Mathf.CeilToInt(totalFish / (float)ThreadGroupSize), 1, 1);

            // 3) simular
            compute.SetBuffer(kSim, "_FishRead", ReadBuffer);
            compute.SetBuffer(kSim, "_FishWrite", WriteBuffer);
            compute.SetBuffer(kSim, "_Species", speciesBuffer);
            compute.SetBuffer(kSim, "_Obstacles", obstacleBuffer);
            compute.SetBuffer(kSim, "_CellCount", cellCount);
            compute.SetBuffer(kSim, "_CellFish", cellFish);
            compute.Dispatch(kSim, Mathf.CeilToInt(totalFish / (float)ThreadGroupSize), 1, 1);

            // El buffer recien escrito pasa a ser el de lectura.
            (fishA, fishB) = (fishB, fishA);
        }

        void Render()
        {
            Bounds worldBounds = new Bounds(transform.position, boundsSize + Vector3.one * 4f);

            for (int s = 0; s < species.Count; s++)
            {
                Species sp = species[s];

                MaterialPropertyBlock block = blocks[s];
                block.SetBuffer(FishId, ReadBuffer);
                block.SetInt(SpeciesOffsetId, speciesOffsets[s]);

                var rp = new RenderParams(sp.material)
                {
                    worldBounds = worldBounds,
                    matProps = block,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    layer = gameObject.layer
                };

                Graphics.RenderMeshIndirect(rp, sp.mesh, argsBuffers[s], 1);
            }
        }

        // ------------------------------------------------------------------ Utilidades
        // Objetivo virtual que vaga por la zona con ruido Perlin (si la especie no tiene objetivo propio).
        Vector3 WanderTarget(int index, Species s, Vector3 center, Vector3 extents)
        {
            float t = Time.time * s.wanderSpeed;
            float seed = index * 17.31f;
            Vector3 n = new Vector3(
                Mathf.PerlinNoise(t, seed),
                Mathf.PerlinNoise(t + 41.7f, seed),
                Mathf.PerlinNoise(t + 93.1f, seed));
            n = (n - Vector3.one * 0.5f) * 2f * 1.5f;
            n = Vector3.Max(Vector3.one * -1f, Vector3.Min(Vector3.one, n));
            return center + Vector3.Scale(n, extents * 0.7f);
        }

        Vector3 ClampToBounds(Vector3 p)
        {
            Vector3 c = transform.position;
            Vector3 e = boundsSize * 0.5f;
            return new Vector3(
                Mathf.Clamp(p.x, c.x - e.x, c.x + e.x),
                Mathf.Clamp(p.y, c.y - e.y, c.y + e.y),
                Mathf.Clamp(p.z, c.z - e.z, c.z + e.z));
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 1f);
            Gizmos.DrawWireCube(transform.position, boundsSize);

            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.8f);
            foreach (Obstacle o in obstacles)
            {
                if (o == null || o.transform == null) continue;
                Gizmos.DrawWireSphere(o.transform.position, o.radius);
                Gizmos.DrawWireSphere(o.transform.position, o.radius + obstacleMargin);
            }
        }
    }
}
