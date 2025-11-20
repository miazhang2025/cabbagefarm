using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Random = UnityEngine.Random;

namespace RoomGen
{
    [RequireComponent(typeof(PresetEditorComponent))]
    [RequireComponent(typeof(EventSystem))]
public class RoomGenerator : MonoBehaviour
{
    public RoomPreset preset;
    public bool debug;
    public int id;

    [Space, Header("Room Size")]
    [Range(1, 20)] public int gridX = 2;
    [Range(1, 20)] public int gridZ = 2;

    public List<Level> levels = new List<Level>();

    [Range(0.01f, 10f)] public float tileSize = 5f;

    [HideInInspector] public List<Roof> roofs = new List<Roof>();
    [HideInInspector] public List<Floor> floors = new List<Floor>();
    [HideInInspector] public List<Wall> walls = new List<Wall>();
    [HideInInspector] public List<Wall> wallCorners = new List<Wall>();
    [HideInInspector] public List<Door> doors = new List<Door>();
    [HideInInspector] public List<Window> windows = new List<Window>();

    [HideInInspector] public List<Decoration> characters = new List<Decoration>();
    [HideInInspector] public List<Decoration> roofDecorations = new List<Decoration>();
    [HideInInspector] public List<Decoration> floorDecorations = new List<Decoration>();
    [HideInInspector] public List<Decoration> wallDecorations = new List<Decoration>();

    [HideInInspector] public GameObject parent;
    [HideInInspector] public GameObject floorTileParent;
    [HideInInspector] public GameObject wallTileParent;
    [HideInInspector] public GameObject roofTileParent;
    [HideInInspector] public GameObject doorParent;
    [HideInInspector] public GameObject windowParent;
    [HideInInspector] public GameObject floorDecorParent;
    [HideInInspector] public GameObject wallDecorParent;
    [HideInInspector] public GameObject roofDecorParent;
    [HideInInspector] public GameObject characterParent;

    List<Node> nodes = new List<Node>();
    public List<GameObject> tiles = new List<GameObject>();
    HashSet<DecoratorPoint> decoratorPoints = new HashSet<DecoratorPoint>();
    List<GameObject> generatedCharacters = new List<GameObject>();
    List<GameObject> generatedWallDecor = new List<GameObject>();
    List<GameObject> generatedFloorDecor = new List<GameObject>();

    private List<DoorPin> doorPins = new List<DoorPin>();

    [Tooltip("Adjust all wall decorations forward by this value.")]
    public float wallDecorationOffset = 0.2f;

    [Tooltip("Adjust all floor decorations up by this value.")]
    public float floorDecorationOffset = 0f;

    [Tooltip("Give floor props a safe area distance from surrounding walls. Nothing will spawn within this distance.")]
    [Range(0, 25)] public int decorSafeArea = 1;

    [HideInInspector] public int points;

    [Tooltip("Multiples available decoration points. Too high may impact performance.")]
    [Range(0, 3)] public int pointSpacing = 1;

    [Range(0.25f, 5f)] public float floorDecorSpacing = 0.5f;

    // Generation weights
    int totalRoofWeight = 0;
    int totalFloorWeight = 0;
    int totalWallWeight = 0;
    int totalWallCornerWeight = 0;
    int totalDoorWeight = 0;
    int totalWindowWeight = 0;

    

    #region Generation

    public void GenerateRoom(int generatorID)
    {
        if (generatorID != this.id)
            return;

        DestroyRoom();
        GenerateLevels();

        if (HasDuplicates(decoratorPoints))
        {
            Debug.LogWarning("Decorator points contain duplicates.");
        }
    }

    bool HasDuplicates(HashSet<DecoratorPoint> points)
    {
        HashSet<Vector3> pointSet = new HashSet<Vector3>();
        foreach (DecoratorPoint dp in points)
        {
            if (!pointSet.Add(dp.point))
                return true;
        }
        return false;
    }

    private void GenerateDecorParent(ref GameObject reference, string name)
    {
        if (reference == null)
        {
            reference = new GameObject(name);
            reference.transform.parent = parent.transform;
        }
    }

    public void GenerateLevels()
    {
        if (parent == null)
        {
            parent = new GameObject("RoomPreview");
            parent.transform.position = transform.position;
        }

        GenerateDecorParent(ref doorParent, "doors");
        GenerateDecorParent(ref windowParent, "windows");
        GenerateDecorParent(ref floorTileParent, "floor tiles");
        GenerateDecorParent(ref wallTileParent, "wall tiles");
        GenerateDecorParent(ref roofTileParent, "roof tiles");
        GenerateDecorParent(ref floorDecorParent, "floor decor");
        GenerateDecorParent(ref wallDecorParent, "wall decor");
        GenerateDecorParent(ref roofDecorParent, "roof decor");
        GenerateDecorParent(ref characterParent, "characters");

        RemoveBounds();

        float offset = 0f;
        foreach (Level level in levels)
        {
            if (level.preset == null)
                continue;

            // Create level bounds
            GameObject levelBounds = new GameObject("LevelBounds");
            levelBounds.transform.position = transform.position;
            levelBounds.transform.parent = transform;
            BoxCollider bounds = levelBounds.AddComponent<BoxCollider>();
            bounds.isTrigger = true;

            bounds.size = new Vector3(gridX * tileSize, level.levelHeight * tileSize, gridZ * tileSize);
            bounds.center = new Vector3(0, offset + (bounds.size.y * 0.5f), 0) + level.levelOffset;
            offset += bounds.size.y;

            // Find door pins for this generator (caching for performance)
            doorPins = FindObjectsOfType<DoorPin>().Where(x => x.roomGenerator == this).ToList();

            CalculateWeights(level);
            SpawnPoints(bounds, level);
            StartCoroutine(SpawnTiles(bounds, level.preset, level));
        }
    }

    public void SpawnPoints(BoxCollider boxCollider, Level level)
    {
        Vector3 min = boxCollider.bounds.min;
        Vector3 max = boxCollider.bounds.max;
        int levelIndex = levels.IndexOf(level);

        // Spawn wall nodes and corners (loop over X and Y)
        for (float x = min.x; x < max.x; x += tileSize)
        {
            for (float y = min.y; y < max.y; y += tileSize)
            {
                if (Mathf.Approximately(x, min.x))
                    nodes.Add(new Node(new Vector3(min.x, y, min.z), Quaternion.Euler(0, 0, 0), TileType.WallCorner, levelIndex));
                if (Mathf.Approximately(x, max.x - tileSize))
                    nodes.Add(new Node(new Vector3(max.x, y, min.z), Quaternion.Euler(0, -90, 0), TileType.WallCorner, levelIndex));

                nodes.Add(new Node(new Vector3(x + tileSize, y, min.z), Quaternion.Euler(0, 0, 0), TileType.Wall, levelIndex));
                nodes.Add(new Node(new Vector3(x, y, max.z), Quaternion.Euler(0, 180, 0), TileType.Wall, levelIndex));
            }
        }

        // Spawn wall nodes and corners (loop over Z and Y)
        for (float z = min.z; z < max.z; z += tileSize)
        {
            for (float y = min.y; y < max.y; y += tileSize)
            {
                if (Mathf.Approximately(z, min.z))
                    nodes.Add(new Node(new Vector3(max.x, y, max.z), Quaternion.Euler(0, 180, 0), TileType.WallCorner, levelIndex));
                if (Mathf.Approximately(z, max.z - tileSize))
                    nodes.Add(new Node(new Vector3(min.x, y, max.z), Quaternion.Euler(0, 90, 0), TileType.WallCorner, levelIndex));

                nodes.Add(new Node(new Vector3(min.x, y, z), Quaternion.Euler(0, 90, 0), TileType.Wall, levelIndex));
                nodes.Add(new Node(new Vector3(max.x, y, z + tileSize), Quaternion.Euler(0, -90, 0), TileType.Wall, levelIndex));
            }
        }

        // Spawn floor nodes
        for (float x = min.x; x < max.x; x += tileSize)
        {
            for (float z = min.z; z < max.z; z += tileSize)
            {
                for (float y = min.y; y < max.y; y += tileSize * level.levelHeight)
                {
                    nodes.Add(new Node(new Vector3(x, y, z), Quaternion.Euler(0, 90, 0), TileType.Floor, levelIndex));
                }
            }
        }

        // Spawn roof nodes
        for (float x = min.x; x < max.x; x += tileSize)
        {
            for (float z = min.z; z < max.z; z += tileSize)
            {
                nodes.Add(new Node(new Vector3(x, max.y, z), Quaternion.Euler(0, 90, 0), TileType.Roof, levelIndex));
            }
        }
    }

    void CalculatePoints()
    {
        int pointsMax = (int)tileSize + 1;
        int numPoints = (pointsMax * pointSpacing) - (pointSpacing - 1);
        points = numPoints;
    }

    //Decorator point spawners

    void SpawnWallDecoratorPoints(BoxCollider boxCollider, GameObject obj, Tile tile, PointType pointType, int levelNumber)
    {
        if (boxCollider == null)
            return;

        Vector3 max = boxCollider.bounds.max;
        Vector3 min = boxCollider.bounds.min;
        CalculatePoints();

        for (int x = 0; x < points - 1; x++)
        {
            for (int y = 0; y < points; y++)
            {
                Vector3 raypos = new Vector3(-x, y, 0);
                Vector3 adjustedPos = Tools.AdjustedPosition(obj, -tile.positionOffset);
                Vector3 rayEnd = adjustedPos + obj.transform.rotation * (raypos / pointSpacing);

                // Skip positions that are exactly at the corners.
                if ((Mathf.Approximately(rayEnd.x, max.x) && Mathf.Approximately(rayEnd.z, min.z)) ||
                    (Mathf.Approximately(rayEnd.x, max.x) && Mathf.Approximately(rayEnd.z, max.z)) ||
                    (Mathf.Approximately(rayEnd.x, min.x) && Mathf.Approximately(rayEnd.z, max.z)) ||
                    (Mathf.Approximately(rayEnd.x, min.x) && Mathf.Approximately(rayEnd.z, min.z)))
                {
                    continue;
                }

                DecoratorPoint newPoint = new DecoratorPoint(obj, null, rayEnd + obj.transform.forward * wallDecorationOffset, pointType, false, levelNumber);
                decoratorPoints.Add(newPoint);
            }
        }
    }

    void SpawnFloorRoofDecoratorPoints(BoxCollider boxCollider, GameObject obj, Tile tile, PointType pointType, int levelNumber)
    {
        if (boxCollider == null)
            return;

        CalculatePoints();

        for (int x = 0; x < points; x++)
        {
            for (int z = 0; z < points; z++)
            {
                // Compute normalized parameters from 0 to 1.
                float tX = (points > 1) ? (float)x / (points - 1) : 0.5f;
                float tZ = (points > 1) ? (float)z / (points - 1) : 0.5f;
                
                float localX = Mathf.Lerp(-tileSize / 2f, tileSize / 2f, tX);
                float localZ = Mathf.Lerp(-tileSize / 2f, tileSize / 2f, tZ);
                Vector3 localPos = new Vector3(localX, 0, localZ);


                Vector3 adjustedPoint = obj.transform.TransformPoint(localPos + new Vector3(0, floorDecorationOffset, 0));

                Vector3 min = boxCollider.bounds.min;
                Vector3 max = boxCollider.bounds.max;
                
                if (adjustedPoint.x < min.x || adjustedPoint.x > max.x || adjustedPoint.z < min.z || adjustedPoint.z > max.z)
                {
                    continue;
                }
                
                if (!WallDistanceCheck(adjustedPoint, GetSafeAreaBounds(boxCollider)))
                    continue;

                // If alignToSurface is enabled perform the raycast, otherwise add directly.
                if (tile.alignToSurface)
                {
                    Vector3 rayOrigin = (pointType == PointType.Roof) ? Vector3.down * 3f : Vector3.up * 1f;
                    Vector3 rayStart = obj.transform.TransformPoint(localPos) + rayOrigin;
                    Ray ray = (pointType == PointType.Roof) ? new Ray(rayStart, Vector3.up) : new Ray(rayStart, Vector3.down);
                    
                    Physics.SyncTransforms();

                    if (Physics.Raycast(ray, out RaycastHit hit, 100f, layerMask: tile.tileLayer))
                    {
                        Vector3 roundedHitPoint = new Vector3(
                            Mathf.Round(hit.point.x * 1000f) / 1000f,
                            Mathf.Round(hit.point.y * 1000f) / 1000f,
                            Mathf.Round(hit.point.z * 1000f) / 1000f);
                        DecoratorPoint newRaycastPoint = new DecoratorPoint(obj, null, roundedHitPoint, pointType, false, levelNumber);
                        decoratorPoints.Add(newRaycastPoint);
                    }
                    
                    // bool didHit = Physics.Raycast(ray, 1000f);
                    // Debug.DrawRay(ray.origin, ray.direction * 10f, didHit ? Color.green : Color.magenta, 4f);

                }
                else
                {
                    // Round the adjustedPoint to 3 decimal places.
                    Vector3 roundedPoint = new Vector3(Mathf.Round(adjustedPoint.x * 1000f) / 1000f, Mathf.Round(adjustedPoint.y * 1000f) / 1000f, Mathf.Round(adjustedPoint.z * 1000f) / 1000f);
                    
                    DecoratorPoint newPoint = new DecoratorPoint(obj, null, roundedPoint, pointType, false, levelNumber);
                    decoratorPoints.Add(newPoint);

                }
            }
        }
    }

    IEnumerator SpawnTiles(BoxCollider boxCollider, RoomPreset preset, Level level)
    {
        // Reset random seeds per level.
        Random.InitState(level.decorSeed);
        Random.InitState(level.roomSeed);

        if (level.levelHeight > 0)
        {
            SpawnDoors(boxCollider, level);
            SpawnWindows(boxCollider, level);
        }

        SpawnWallsAndFloors(boxCollider, preset, levels.IndexOf(level));

        yield return new WaitForSeconds(0.0001f);

        Decorate(boxCollider, level, level.preset.wallDecorations, PointType.Wall, DecorationType.Wall, wallDecorParent);
        Decorate(boxCollider, level, level.preset.floorDecorations, PointType.Floor, DecorationType.Floor, floorDecorParent);
        Decorate(boxCollider, level, level.preset.roofDecorations, PointType.Roof, DecorationType.Roof, roofDecorParent);
        Decorate(boxCollider, level, level.preset.characters, PointType.Floor, DecorationType.Character, characterParent);
    }

    void SpawnWallsAndFloors(BoxCollider boxCollider, RoomPreset preset, int levelNumber)
    {
        foreach (Node node in nodes)
        {
            if (!node.isAvailable || node.levelNumber != levelNumber)
                continue;

            switch (node.tileType)
            {
                case TileType.WallCorner:
                    {
                        Wall wall = Tools.GetWeightedWall(preset.wallCorners, totalWallCornerWeight);
                        if (wall == null || wall.prefab == null)
                            break;
                        GameObject obj = InstantiatePrefab(wall.prefab);
                        SetupTile(obj, node.position, node.rotation, wall.positionOffset, wall.rotationOffset);
                        obj.transform.parent = wallTileParent.transform;
                        tiles.Add(obj);
                        break;
                    }
                case TileType.Wall:
                    {
                        Wall wall = Tools.GetWeightedWall(preset.wallTiles, totalWallWeight);
                        if (wall == null || wall.prefab == null)
                            break;
                        GameObject obj = InstantiatePrefab(wall.prefab);
                        SetupTile(obj, node.position, node.rotation, wall.positionOffset, wall.rotationOffset);
                        obj.transform.parent = wallTileParent.transform;
                        tiles.Add(obj);
                        if (wall.allowDecor)
                            SpawnWallDecoratorPoints(boxCollider, obj, wall, PointType.Wall, levelNumber);
                        break;
                    }
                case TileType.Floor:
                    {
                        Floor floor = Tools.GetWeightedFloor(preset.floorTiles, totalFloorWeight);
                        if (floor == null || floor.prefab == null)
                            break;
                        GameObject obj = InstantiatePrefab(floor.prefab);
                        int randomRotation = UnityEngine.Random.Range(0, floor.randomRotation + 1);
                        obj.transform.position = node.position;
                        obj.transform.rotation = node.rotation;
                        Tools.AdjustPosition(obj, floor.positionOffset, true, randomRotation);
                        Tools.AdjustRotation(obj, floor.rotationOffset, true, randomRotation);
                        obj.transform.parent = floorTileParent.transform;
                        tiles.Add(obj);
                        if (floor.allowDecor)
                            SpawnFloorRoofDecoratorPoints(boxCollider, obj, floor, PointType.Floor, levelNumber);
                        break;
                    }
                case TileType.Roof:
                    {
                        Roof roof = Tools.GetWeightedRoof(preset.roofTiles, totalRoofWeight);
                        if (roof == null || roof.prefab == null)
                            break;
                        GameObject obj = InstantiatePrefab(roof.prefab);
                        int randomRotation = UnityEngine.Random.Range(0, roof.randomRotation + 1);
                        obj.transform.position = node.position;
                        obj.transform.rotation = node.rotation;
                        Tools.AdjustPosition(obj, roof.positionOffset, true, randomRotation);
                        Tools.AdjustRotation(obj, roof.rotationOffset, true, randomRotation);
                        obj.transform.parent = roofTileParent.transform;
                        tiles.Add(obj);
                        if (roof.allowDecor)
                            SpawnFloorRoofDecoratorPoints(boxCollider, obj, roof, PointType.Roof, levelNumber);
                        break;
                    }
            }
        }
    }

    // --- Helper methods ---

    GameObject InstantiatePrefab(GameObject prefab)
    {
    #if UNITY_EDITOR
        return PrefabUtility.InstantiatePrefab(prefab) as GameObject;
    #else
        return Instantiate(prefab) as GameObject;
    #endif
    }

    void SetupTile(GameObject obj, Vector3 position, Quaternion rotation, Vector3 posOffset, Vector3 rotOffset)
    {
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        Tools.AdjustPosition(obj, posOffset);
        Tools.AdjustRotation(obj, rotOffset);
    }

    Node RandomNode(List<float> heights, TileType tileType)
    {
        List<Node> matchingNodes = new List<Node>();
        foreach (Node node in nodes)
        {
            if (node.isAvailable && node.tileType == tileType)
            {
                foreach (float h in heights)
                {
                    if (Mathf.Abs(node.position.y - h) < 0.01f)
                    {
                        matchingNodes.Add(node);
                        break;
                    }
                }
            }
        }
        if (matchingNodes.Count > 0)
            return matchingNodes[UnityEngine.Random.Range(0, matchingNodes.Count)];
        return null;
    }

    Node FindClosestNode(Vector3 referencePoint, float levelYMin, TileType tileType)
    {
        Node closestNode = null;
        float closestDistance = Mathf.Infinity;
        foreach (Node node in nodes)
        {
            if (node.isAvailable && Mathf.Abs(node.position.y - levelYMin) < 0.01f && node.tileType == tileType)
            {
                float distance = Vector3.Distance(referencePoint, node.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestNode = node;
                }
            }
        }
        return closestNode;
    }

    void SpawnDoors(BoxCollider boxCollider, Level level)
    {
        int levelNumber = levels.IndexOf(level);
        float yMin = boxCollider.bounds.min.y;
        List<float> floorHeights = new List<float> { yMin };

        // Get door pins
        List<DoorPin> doorPinsForLevel = new List<DoorPin>();
        foreach (DoorPin dp in doorPins)
        {
            if (dp.transform.position.y >= boxCollider.bounds.min.y && dp.transform.position.y <= boxCollider.bounds.max.y)
                doorPinsForLevel.Add(dp);
        }

        // For every door pin:
        foreach (DoorPin doorPin in doorPinsForLevel)
        {
            Node node = FindClosestNode(doorPin.transform.position, yMin, TileType.Wall);
            Door door = Tools.GetWeightedDoor(level.preset.doorTiles, totalDoorWeight);
            if (door == null || door.prefab == null)
                continue;

            GameObject doorObj = InstantiatePrefab(door.prefab);
            SetupTile(doorObj, node.position, node.rotation, door.positionOffset, door.rotationOffset);
            doorObj.transform.parent = doorParent.transform;
            tiles.Add(doorObj);
            node.isAvailable = false;

            if (door.allowDecor)
                SpawnWallDecoratorPoints(boxCollider, doorObj, door, PointType.Door, levelNumber);
        }

        // Place remaining doors randomly.
        int remainingDoors = level.numDoors - doorPinsForLevel.Count;
        for (int i = 0; i < remainingDoors; i++)
        {
            Node node = RandomNode(floorHeights, TileType.Wall);
            if (node == null)
                continue;

            Door door = Tools.GetWeightedDoor(level.preset.doorTiles, totalDoorWeight);
            if (door == null || door.prefab == null)
                continue;

            GameObject doorObj = InstantiatePrefab(door.prefab);
            SetupTile(doorObj, node.position, node.rotation, door.positionOffset, door.rotationOffset);
            doorObj.transform.parent = doorParent.transform;
            tiles.Add(doorObj);
            node.isAvailable = false;

            if (door.allowDecor)
                SpawnWallDecoratorPoints(boxCollider, doorObj, door, PointType.Door, levelNumber);
        }
    }

    void SpawnWindows(BoxCollider boxCollider, Level level)
    {
        int levelNumber = levels.IndexOf(level);
        float yMin = boxCollider.bounds.min.y;
        List<float> floorHeights = new List<float>();
        float wHeight = yMin + ((level.windowHeight - 1) * tileSize);
        floorHeights.Add(wHeight);

        for (int i = 0; i < level.numWindows; i++)
        {
            Node node = RandomNode(floorHeights, TileType.Wall);
            if (node == null)
                continue;

            Window window = Tools.GetWeightedWindow(level.preset.windowTiles, totalWindowWeight);
            if (window == null || window.prefab == null)
                continue;

            GameObject windowObj = InstantiatePrefab(window.prefab);
            SetupTile(windowObj, node.position, node.rotation, window.positionOffset, window.rotationOffset);
            windowObj.transform.parent = windowParent.transform;
            tiles.Add(windowObj);
            node.isAvailable = false;

            if (window.allowDecor)
                SpawnWallDecoratorPoints(boxCollider, windowObj, window, PointType.Wall, levelNumber);
        }
    }


    void Decorate(BoxCollider boxCollider, Level level, List<Decoration> decorList, PointType pointType, DecorationType decorType, GameObject objParent)
    {
        int levelNumber = levels.IndexOf(level);
        if (boxCollider == null || decorList.Count == 0)
            return;

        int seed = (decorType == DecorationType.Character) ? level.characterSeed : level.decorSeed;
        System.Random random = new System.Random(seed);
        Bounds bounds = boxCollider.bounds;

        // Precompute a list of decorator points to check (for spacing and safe area) based on the point type.
        List<DecoratorPoint> pointsToCheck = new List<DecoratorPoint>();
        foreach (DecoratorPoint dp in decoratorPoints)
        {
            if (dp.levelNumber != levelNumber)
                continue;

            if (pointType == PointType.Character || pointType == PointType.Floor)
            {
                if (dp.pointType == PointType.Floor || dp.pointType == PointType.Character)
                    pointsToCheck.Add(dp);
            }
            else if (pointType == PointType.Wall)
            {
                if (dp.pointType == PointType.Wall || dp.pointType == PointType.Character)
                    pointsToCheck.Add(dp);
            }
        }

        foreach (Decoration decoration in decorList)
        {
            int randomAmount = random.Next(Mathf.Min(decoration.amountRange.x, decoration.amountRange.y), decoration.amountRange.y);
            float tolerance = 0.01f;

            float minNodeHeight, maxNodeHeight;
            if (pointType == PointType.Floor)
            {
                // Expand the range slightly so that decor points that are a bit above the floor are accepted.
                minNodeHeight = decoration.verticalRange.x + bounds.min.y - tolerance;
                maxNodeHeight = decoration.verticalRange.y + bounds.min.y + tolerance;
            }
            else
            {
                minNodeHeight = decoration.verticalRange.x + bounds.min.y;
                maxNodeHeight = decoration.verticalRange.y + bounds.min.y;
            }

            List<DecoratorPoint> validPoints = new List<DecoratorPoint>();

            // Cache valid decorator points that fall within the vertical range and aren't occupied.
            foreach (DecoratorPoint point in decoratorPoints)
            {
                if (!point.occupied && point.pointType == pointType && point.levelNumber == levelNumber && point.point.y >= minNodeHeight && point.point.y <= maxNodeHeight)
                {
                    validPoints.Add(point);
                }
            }

            for (int decorCount = 0; decorCount < randomAmount; decorCount++)
            {
                if (validPoints.Count == 0)
                    break; // No more available points.

                // Select a random candidate from the validPoints list.
                int randomIndex = random.Next(validPoints.Count);
                DecoratorPoint randomPoint = validPoints[randomIndex];
                if (randomPoint == null || decoration.prefab == null)
                    continue;

                // Remove the selected point so it won’t be reused.
                validPoints.RemoveAt(randomIndex);

                // Remove (from the validPoints list) any candidate that is too close to the chosen point based on spacing.
                for (int i = validPoints.Count - 1; i >= 0; i--)
                {
                    if (Vector3.Distance(validPoints[i].point, randomPoint.point) <= decoration.spacing)
                    {
                        validPoints.RemoveAt(i);
                    }
                }

                // Additionally, iterate over ALL decorator points for the current level and type.
                // Mark any point that lies within the decoration's safe area as occupied so they won't be used later.
                foreach (DecoratorPoint dp in decoratorPoints)
                {
                    if (dp.levelNumber == levelNumber && dp.pointType == pointType && !dp.occupied)
                    {
                        if (Vector3.Distance(dp.point, randomPoint.point) <= decoration.safeArea)
                        {
                            dp.occupied = true;
                        }
                    }
                }

                // Mark the chosen candidate as occupied.
                randomPoint.occupied = true;

                // Instantiate the decoration at the chosen point.
                GameObject decor = InstantiatePrefab(decoration.prefab);
                decor.transform.position = randomPoint.point;
                if (randomPoint.tileObject != null)
                    decor.transform.rotation *= randomPoint.tileObject.transform.rotation;
                decor.transform.rotation *= Quaternion.Euler(decoration.rotationOffset);
                Tools.AdjustPosition(decor, decoration.positionOffset);
                float rotationRandomValue = (float)random.NextDouble() * decoration.randomRotation;
                if (pointType == PointType.Wall)
                    decor.transform.Rotate(transform.forward * rotationRandomValue, Space.Self);
                else
                    decor.transform.Rotate(Vector3.up * rotationRandomValue, Space.World);
                float scaleRandomValue = (float)(random.NextDouble() * (decoration.scaleRange.y - decoration.scaleRange.x) + decoration.scaleRange.x);
                decor.transform.localScale *= scaleRandomValue;
                decor.transform.parent = objParent.transform;
                tiles.Add(decor);
            }

        }
    }

    #endregion

    #region SafeArea Helpers

    private struct SafeAreaBounds
    {
        public float xMin, xMax, zMin, zMax;
    }
    
    private SafeAreaBounds GetSafeAreaBounds(BoxCollider collider)
    {
        return new SafeAreaBounds
        {
            xMin = collider.bounds.min.x + decorSafeArea * 0.5f,
            xMax = collider.bounds.max.x - decorSafeArea * 0.5f,
            zMin = collider.bounds.min.z + decorSafeArea * 0.5f,
            zMax = collider.bounds.max.z - decorSafeArea * 0.5f
        };
    }

    private bool WallDistanceCheck(Vector3 gridPoint, SafeAreaBounds bounds)
    {
        const float tolerance = 0.1f;
        return gridPoint.x >= bounds.xMin - tolerance && gridPoint.x <= bounds.xMax + tolerance &&
               gridPoint.z >= bounds.zMin - tolerance && gridPoint.z <= bounds.zMax + tolerance;
    }

    #endregion

    #region Calculate Weights
    // Calculate weights for roof, floor, wall, door, and window tiles.
    void CalculateWeights(Level level)
    {
        totalRoofWeight = 0;
        totalFloorWeight = 0;
        totalWallWeight = 0;
        totalWallCornerWeight = 0;
        totalDoorWeight = 0;
        totalWindowWeight = 0;

        foreach (Roof roof in level.preset.roofTiles)
            totalRoofWeight += roof.weight;

        foreach (Floor floor in level.preset.floorTiles)
            totalFloorWeight += floor.weight;

        foreach (Wall wall in level.preset.wallTiles)
            totalWallWeight += wall.weight;

        foreach (Wall wallCorner in level.preset.wallCorners)
            totalWallCornerWeight += wallCorner.weight;

        foreach (Door door in level.preset.doorTiles)
            totalDoorWeight += door.weight;

        foreach (Window window in level.preset.windowTiles)
            totalWindowWeight += window.weight;
    }
    #endregion

    #region Event Subscription

    void SubscribeToEvents()
    {
        EventSystem.instance.OnGenerate += GenerateRoom;
        EventSystem.instance.OnSetRoomPreset += SetRoomPreset;
        EventSystem.instance.OnSetGridSize += SetGridSize;
        EventSystem.instance.OnSetRoomSeed += SetRoomSeed;
        EventSystem.instance.OnSetDecorSeed += SetDecorSeed;
        EventSystem.instance.OnSetCharacterSeed += SetCharacterSeed;
        EventSystem.instance.OnSetDoorCount += SetDoorCount;
        EventSystem.instance.OnSetWindowCount += SetWindowCount;
        EventSystem.instance.OnSetLevelHeight += SetLevelHeight;
        EventSystem.instance.OnSetLevelOffset += SetLevelOffset;
    }

    void UnsubscribeFromEvents()
    {
        EventSystem.instance.OnGenerate -= GenerateRoom;
        EventSystem.instance.OnSetRoomPreset -= SetRoomPreset;
        EventSystem.instance.OnSetGridSize -= SetGridSize;
        EventSystem.instance.OnSetRoomSeed -= SetRoomSeed;
        EventSystem.instance.OnSetDecorSeed -= SetDecorSeed;
        EventSystem.instance.OnSetCharacterSeed -= SetCharacterSeed;
        EventSystem.instance.OnSetDoorCount -= SetDoorCount;
        EventSystem.instance.OnSetWindowCount -= SetWindowCount;
        EventSystem.instance.OnSetLevelHeight -= SetLevelHeight;
        EventSystem.instance.OnSetLevelOffset -= SetLevelOffset;
    }

    private void SetRoomPreset(int generatorID, RoomPreset newPreset, int levelNumber)
    {
        if (generatorID != this.id) return;
        if (levels.Count > levelNumber)
            levels[levelNumber].preset = newPreset;
        else
            Debug.LogWarning($"Attempted to set a new preset on level: {levelNumber}, but that level didn't exist.", gameObject);
    }

    private void SetGridSize(int generatorID, int x, int z)
    {
        if (generatorID != this.id) return;
        if (x <= 0 || z <= 0)
        {
            Debug.LogWarning("RoomGen grid size values must be greater than or equal to 1.");
            return;
        }
        gridX = x;
        gridZ = z;
    }

    private void SetRoomSeed(int generatorID, int levelNumber, int seed)
    {
        if (generatorID != this.id) return;
        if (LevelIndexCheck("room seed", levelNumber))
            levels[levelNumber].roomSeed = seed;
    }

    private void SetDecorSeed(int generatorID, int levelNumber, int seed)
    {
        if (generatorID != this.id) return;
        if (LevelIndexCheck("decor seed", levelNumber))
            levels[levelNumber].decorSeed = seed;
    }

    private void SetCharacterSeed(int generatorID, int levelNumber, int seed)
    {
        if (generatorID != this.id) return;
        if (LevelIndexCheck("character seed", levelNumber))
            levels[levelNumber].characterSeed = seed;
    }

    private void SetDoorCount(int generatorID, int levelNumber, int count)
    {
        if (generatorID != this.id) return;
        if (LevelIndexCheck("door count", levelNumber))
            levels[levelNumber].numDoors = count;
    }

    private void SetWindowCount(int generatorID, int levelNumber, int count)
    {
        if (generatorID != this.id) return;
        if (LevelIndexCheck("window count", levelNumber))
            levels[levelNumber].numWindows = count;
    }

    private void SetLevelHeight(int generatorID, int levelNumber, int height)
    {
        if (generatorID != this.id) return;
        if (LevelIndexCheck("level height", levelNumber))
            levels[levelNumber].levelHeight = height;
    }

    private void SetLevelOffset(int generatorID, int levelNumber, Vector3 offset)
    {
        if (generatorID != this.id) return;
        if (LevelIndexCheck("level offset", levelNumber))
            levels[levelNumber].levelOffset = offset;
    }

    private bool LevelIndexCheck(string detail, int levelNumber)
    {
        if (levels.Count - 1 < levelNumber)
        {
            Debug.LogWarning($"Attempted to adjust {detail} for level {levelNumber} but no matching level exists. Levels start at index 0.");
            return false;
        }
        return true;
    }

    #endregion

    private void OnEnable() { SubscribeToEvents(); }
    private void OnDisable() { UnsubscribeFromEvents(); }

    public void RemoveBounds()
    {
        // Destroy all children used as bounds.
        foreach (Transform child in transform)
        {
            DestroyImmediate(child.gameObject);
        }
    }

    public void DestroyRoom()
    {
        foreach (GameObject tile in tiles)
        {
            DestroyImmediate(tile);
        }
        nodes.Clear();
        decoratorPoints.Clear();
        generatedWallDecor.Clear();
        generatedFloorDecor.Clear();
        generatedCharacters.Clear();
        tiles.Clear();
        RemoveBounds();
    }


        #region inspectorValidations;

        public void UpdateStoredValues()
        {
            walls = preset.wallTiles;
            doors = preset.doorTiles;
            floors = preset.floorTiles;
            windows = preset.windowTiles;
            characters = preset.characters;
            wallDecorations = preset.wallDecorations;
            floorDecorations = preset.floorDecorations;

            // save them to the preset.
            UpdatePreset();
        }

        public void UpdatePreset()
        {
            preset.wallTiles = walls;
            preset.doorTiles = doors;
            preset.floorTiles = floors;
            preset.windowTiles = windows;
            preset.characters = characters;
            preset.wallDecorations = wallDecorations;
            preset.floorDecorations = floorDecorations;
        }

        #endregion


        private void OnDrawGizmos()
        {
            if (!debug)
                return;
            foreach (BoxCollider boxCollider in GetComponentsInChildren<BoxCollider>())
            {
                // Vector3 max = boxCollider.bounds.max;
                // Vector3 min = boxCollider.bounds.min;
                //
                // foreach (Node point in nodes)
                // {
                //     Gizmos.color = Color.green;
                //     Gizmos.DrawSphere(point.position, 0.125f);
                // }
                //
                //
                // Gizmos.color = Color.red;
                // Gizmos.DrawSphere(max, 0.21f);
                //
                // Gizmos.color = Color.blue;
                // Gizmos.DrawSphere(min, 0.21f);
            }

            // foreach (var point in safeAreaPoints)
            // {
            //     Gizmos.color = Color.red;
            //     Gizmos.DrawSphere(point, 0.125f);
            // }

            // foreach (Node node in nodes)
            // {
            //     if (node.tileType == TileType.WallCorner)
            //     {
            //         Gizmos.color = Color.red;
            //         Gizmos.DrawSphere(node.position, 0.125f);
            //     }
            //
            //     if (node.tileType == TileType.Floor)
            //     {
            //         Gizmos.color = Color.green;
            //         Gizmos.DrawSphere(node.position, 0.125f);
            //     }
            //
            //     if (node.tileType == TileType.Roof)
            //     {
            //         Gizmos.color = Color.yellow;
            //         Gizmos.DrawSphere(node.position, 0.125f);
            //     }
            // }

            foreach (DecoratorPoint point in decoratorPoints)
            {
                if (point.pointType == PointType.Wall)
                {
                    Gizmos.color = point.occupied ? Color.red : Color.white;
                    Gizmos.DrawWireSphere(point.point, 0.125f);
                }

                if (point.pointType == PointType.Floor)
                {
                    Gizmos.color = point.occupied ? Color.red : Color.green;
                    Gizmos.DrawWireSphere(point.point, 0.125f);
                }

                if (point.pointType == PointType.Window)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(point.point, 0.125f);
                }

                if (point.pointType == PointType.Door)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(point.point, 0.125f);
                }

                if (point.pointType == PointType.Roof)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawWireSphere(point.point, 0.125f);
                }
            }
        }


        public void Save()
        {
            generatedFloorDecor.Clear();
            generatedWallDecor.Clear();
            decoratorPoints.Clear();
            DestroyImmediate(parent);


            foreach (Transform child in transform)
            {
                DestroyImmediate(child.gameObject);
            }

            Debug.Log("room prefab saved.");
        }
    }

    [System.Serializable]
    public enum TileType
    {
        Floor,
        Wall,
        WallCorner,
        Roof
    }

    [System.Serializable]
    public enum DecorationType
    {
        Door,
        Window,
        Wall,
        Floor,
        Character,
        Roof
    }
}