using System.Collections.Generic;
using System.IO;
using System.Text;
using _Works.CJW.Scripts.Customers.Visit;
using _Works.CJW.Scripts.MapSystems;
using DevLib.ObjectPool.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>플레이하지 않고 열린 씬만 보고 손님 방문을 따라가 볼 재료를 뽑는다.
    /// 지점 목록과 사람 NavMesh 위 여부, 주차 자리에서 각 지점까지 걸어갈 수 있는지, 스폰 목록, 손님별 단계 시퀀스를 Temp/CustomerSim/static.txt에 쓴다.</summary>
    public static class CustomerStaticSimReport
    {
        private const string OutPath = "Temp/CustomerSim/static.txt";
        private const float SampleRadius = 3f;

        [MenuItem("Tools/CJW/Static Customer Sim Report")]
        private static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}");

            int humanAgent = FindCustomerAgentType(out string agentSource);
            sb.AppendLine($"Customer agentTypeID={humanAgent} ({NavMesh.GetSettingsNameFromID(humanAgent)}) from {agentSource}");

            // 지점
            MapPosition[] points = Object.FindObjectsByType<MapPosition>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var byType = new Dictionary<MapPointType, List<MapPosition>>();
            sb.AppendLine();
            sb.AppendLine("== Map points");
            foreach (MapPosition p in points)
            {
                string onMesh = Sample(p.Position, humanAgent, out Vector3 snapped) ? $"navmesh d={Vector3.Distance(p.Position, snapped):0.00}" : "NO NAVMESH within 3m";
                string data = p.MapData != null ? p.MapData.name : "NULL";
                sb.AppendLine($"{p.Type,-15} {Path(p.transform),-60} pos={Fmt(p.Position)} active={p.isActiveAndEnabled} data={data} {onMesh}");
                if (!byType.TryGetValue(p.Type, out List<MapPosition> list))
                {
                    byType[p.Type] = list = new List<MapPosition>();
                }

                list.Add(p);
            }

            sb.AppendLine();
            sb.AppendLine("== Point counts");
            foreach (MapPointType t in System.Enum.GetValues(typeof(MapPointType)))
            {
                sb.AppendLine($"{t,-15} {(byType.TryGetValue(t, out List<MapPosition> l) ? l.Count : 0)}");
            }

            // 주차 자리 → 각 지점 도달
            sb.AppendLine();
            sb.AppendLine("== Walk reachability (humanoid) from each ParkingSlot to nearest point of each type");
            if (byType.TryGetValue(MapPointType.ParkingSlot, out List<MapPosition> slots))
            {
                foreach (MapPosition slot in slots)
                {
                    sb.AppendLine($"-- from {slot.name} {Fmt(slot.Position)}");
                    if (!Sample(slot.Position, humanAgent, out Vector3 from, 6f))
                    {
                        sb.AppendLine("   slot has no humanoid navmesh within 6m");
                        continue;
                    }

                    foreach (KeyValuePair<MapPointType, List<MapPosition>> pair in byType)
                    {
                        if (pair.Key == MapPointType.ParkingSlot)
                        {
                            continue;
                        }

                        foreach (MapPosition target in pair.Value)
                        {
                            sb.AppendLine($"   {pair.Key,-15} {target.name,-28} {PathInfo(from, target.Position, humanAgent)}");
                        }
                    }
                }
            }

            ReportVandalApproach(sb, byType, humanAgent);

            // 방문 감독
            sb.AppendLine();
            sb.AppendLine("== VisitDirector");
            var customers = new HashSet<Object>();
            foreach (VisitDirector director in Object.FindObjectsByType<VisitDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(director);
                sb.AppendLine($"{Path(director.transform)} active={director.isActiveAndEnabled}");
                foreach (string field in new[] { "mapData", "spawnPoint", "shopPoint", "exitPoint", "spawnInterval", "maxConcurrentVisits", "autoDepartSeconds", "fuelCustomerChance", "carGradeTable" })
                {
                    sb.AppendLine($"  {field} = {Value(so.FindProperty(field))}");
                }

                foreach (string field in new[] { "spawnPoint", "shopPoint", "exitPoint" })
                {
                    if (so.FindProperty(field)?.objectReferenceValue is Transform tr)
                    {
                        sb.AppendLine($"  {field} humanoid: {(Sample(tr.position, humanAgent, out _) ? "on" : "off")} navmesh");
                    }
                }

                ReportCarRoutes(sb, so, byType);

                SerializedProperty cars = so.FindProperty("carDataList");
                sb.AppendLine($"  carDataList ({cars?.arraySize ?? 0})");
                for (int i = 0; cars != null && i < cars.arraySize; i++)
                {
                    Object car = cars.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (car == null)
                    {
                        sb.AppendLine("    NULL");
                        continue;
                    }

                    var carSo = new SerializedObject(car);
                    SerializedProperty list = carSo.FindProperty("customers");
                    var names = new List<string>();
                    for (int j = 0; list != null && j < list.arraySize; j++)
                    {
                        Object c = list.GetArrayElementAtIndex(j).objectReferenceValue;
                        names.Add(c != null ? c.name : "NULL");
                        if (c != null)
                        {
                            customers.Add(c);
                        }
                    }

                    sb.AppendLine($"    {car.name,-20} grade={Value(carSo.FindProperty("grade"))} w={Value(carSo.FindProperty("spawnWeight"))} max={Value(carSo.FindProperty("maxConcurrent"))} customers=[{string.Join(", ", names)}]");
                }

                SerializedProperty defaults = so.FindProperty("defaultCustomerDataList");
                var defaultNames = new List<string>();
                for (int i = 0; defaults != null && i < defaults.arraySize; i++)
                {
                    Object c = defaults.GetArrayElementAtIndex(i).objectReferenceValue;
                    defaultNames.Add(c != null ? c.name : "NULL");
                    if (c != null)
                    {
                        customers.Add(c);
                    }
                }

                sb.AppendLine($"  defaultCustomerDataList=[{string.Join(", ", defaultNames)}]");
            }

            // 손님
            sb.AppendLine();
            sb.AppendLine("== Customers");
            foreach (Object data in customers)
            {
                var dataSo = new SerializedObject(data);
                sb.AppendLine($"# {data.name}: type={Value(dataSo.FindProperty("<CustomerType>k__BackingField"))} w={Value(dataSo.FindProperty("spawnWeight"))} alone={Value(dataSo.FindProperty("ridesAlone"))} pairs={Value(dataSo.FindProperty("comesInPairs"))}");
                var item = dataSo.FindProperty("poolItem")?.objectReferenceValue as PoolItemSO;
                GameObject prefab = item != null ? item.prefab : null;
                if (prefab == null)
                {
                    sb.AppendLine("  NO PREFAB");
                    continue;
                }

                var fsm = prefab.GetComponentInChildren<Customers.Visit.CustomerFSM.CustomerFSMModule>(true);
                if (fsm == null)
                {
                    sb.AppendLine($"  {prefab.name}: no CustomerFSMModule");
                    continue;
                }

                var fsmSo = new SerializedObject(fsm);
                sb.AppendLine($"  prefab={prefab.name} mapData={Value(fsmSo.FindProperty("mapData"))}");
                SerializedProperty seqs = fsmSo.FindProperty("sequences");
                for (int i = 0; seqs != null && i < seqs.arraySize; i++)
                {
                    SerializedProperty seq = seqs.GetArrayElementAtIndex(i);
                    sb.AppendLine($"  [{Value(seq.FindPropertyRelative("Phase"))}]");
                    SerializedProperty states = seq.FindPropertyRelative("States");
                    for (int j = 0; states != null && j < states.arraySize; j++)
                    {
                        sb.AppendLine($"    {j}. {DescribeRef(states.GetArrayElementAtIndex(j), 6)}");
                    }
                }

                sb.AppendLine($"  combat: {DescribeRef(fsmSo.FindProperty("combat"), 4)}");
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(OutPath));
            File.WriteAllText(OutPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[StaticSim] {OutPath} 작성");
        }

        /// <summary>VandalizeState(MapPoint)를 흉내 낸다. 주차 자리에서 온 손님이 지점에서 standoff만큼 자기 쪽으로 물러난 자리에 서서
        /// 지점의 부모에서 찾은 IVandalTarget을 때린다. 선 자리가 NavMesh 위인지, 맞는 물건 표면까지 몇 m인지 본다.</summary>
        private static void ReportVandalApproach(StringBuilder sb, Dictionary<MapPointType, List<MapPosition>> byType, int humanAgent)
        {
            const float standoff = 0.2f;
            sb.AppendLine();
            sb.AppendLine($"== Vandal approach (VendingMachine, standoff {standoff})");

            if (!byType.TryGetValue(MapPointType.VendingMachine, out List<MapPosition> targets) ||
                !byType.TryGetValue(MapPointType.ParkingSlot, out List<MapPosition> slots))
            {
                sb.AppendLine("  no VendingMachine points or parking slots");
                return;
            }

            foreach (MapPosition target in targets)
            {
                var victim = target.GetComponentInParent<Customers.Interaction.IVandalTarget>() as Component;
                sb.AppendLine($"  {Path(target.transform)} victim={(victim != null ? Path(victim.transform) : "NONE (swing only)")}");
                Collider[] body = victim != null ? victim.GetComponentsInChildren<Collider>() : new Collider[0];

                foreach (MapPosition slot in slots)
                {
                    if (!Sample(slot.Position, humanAgent, out Vector3 from, 6f))
                    {
                        continue;
                    }

                    Vector3 toCustomer = from - target.Position;
                    toCustomer.y = 0f;
                    Vector3 approach = target.Position + toCustomer.normalized * standoff;
                    string stand = Sample(approach, humanAgent, out Vector3 snapped, 2f) ? $"stand {Fmt(snapped)} (moved {Vector3.Distance(approach, snapped):0.00})" : "approach OFF navmesh";

                    float surface = float.MaxValue;
                    foreach (Collider c in body)
                    {
                        if (c.enabled && !c.isTrigger)
                        {
                            Vector3 p = c.ClosestPoint(snapped + Vector3.up);
                            p.y = snapped.y;
                            surface = Mathf.Min(surface, Vector3.Distance(snapped, p));
                        }
                    }

                    string reach = surface < float.MaxValue ? $"to surface {surface:0.00}m" : "no collider";
                    sb.AppendLine($"    from {slot.name}: {PathInfo(from, snapped, humanAgent)} | {stand} | {reach}");
                }
            }
        }

        /// <summary>차가 스폰 → 자리·입구·순회 지점 → 출구로 갈 수 있는지 차 NavMesh로 본다.</summary>
        private static void ReportCarRoutes(StringBuilder sb, SerializedObject director, Dictionary<MapPointType, List<MapPosition>> byType)
        {
            int carAgent = Cars.CarNavMesh.AgentTypeId;
            sb.AppendLine($"  car agentTypeID={carAgent} ({NavMesh.GetSettingsNameFromID(carAgent)})");

            var spawn = director.FindProperty("spawnPoint")?.objectReferenceValue as Transform;
            var exit = director.FindProperty("exitPoint")?.objectReferenceValue as Transform;
            foreach (Transform tr in new[] { spawn, exit })
            {
                if (tr != null)
                {
                    sb.AppendLine($"  car navmesh near {tr.name}{Fmt(tr.position)}: {(Sample(tr.position, carAgent, out Vector3 s, 20f) ? $"nearest {Fmt(s)} d={Vector3.Distance(tr.position, s):0.0}" : "none within 20m")}");
                }
            }

            if (spawn == null || exit == null || !Sample(spawn.position, carAgent, out Vector3 from, 20f))
            {
                return;
            }

            ReportTrafficMap(sb, spawn, exit, from, carAgent, byType);

            // 도로와 부지 사이(울타리) 1m 지도. '='도로 '#'도로 밖 차 NavMesh '.'없음 'f'=Road fence 콜라이더.
            sb.AppendLine("  road side x 4→30, z 64→0, 1m ('=' road '#' mesh '.' none 'f' fence):");
            for (float z = 64f; z >= 0f; z -= 1f)
            {
                var row = new StringBuilder($"    {z,4:0} ");
                for (float x = 4f; x <= 30f; x += 1f)
                {
                    var p = new Vector3(x, 0.1f, z);
                    bool mesh = Sample(p, carAgent, out Vector3 hit, 0.5f) && Vector2.Distance(new Vector2(hit.x, hit.z), new Vector2(x, z)) < 0.4f;
                    bool road = false, fence = false;
                    foreach (RaycastHit rh in Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 60f))
                    {
                        string n = rh.collider.transform.parent != null ? rh.collider.transform.parent.name : "";
                        if (rh.collider.name.ToLower().Contains("road") && !n.Contains("fence") && !rh.collider.name.Contains("fence")) road = true;
                        for (Transform t = rh.collider.transform; t != null; t = t.parent)
                        {
                            if (t.name == "Road fence") fence = true;
                        }
                    }

                    row.Append(fence ? 'f' : mesh ? (road ? '=' : '#') : '.');
                }

                sb.AppendLine(row.ToString());
            }

            // 주차 줄 주변 차 NavMesh 지도. '#'=차가 설 수 있음(0.3m 안에 NavMesh), '.'=없음, 'S'=주차 자리, 'P'=주유기 지점.
            sb.AppendLine("  car navmesh around slots (x 28→46 →, z 50→14 ↓, 1m):");
            for (float z = 50f; z >= 14f; z -= 1f)
            {
                var row = new StringBuilder("    ");
                row.Append($"{z,4:0} ");
                for (float x = 28f; x <= 46f; x += 1f)
                {
                    var probe = new Vector3(x, 0.1f, z);
                    char c = Sample(probe, carAgent, out Vector3 hit, 0.5f) && Vector2.Distance(new Vector2(hit.x, hit.z), new Vector2(x, z)) < 0.3f ? '#' : '.';
                    if (byType.TryGetValue(MapPointType.ParkingSlot, out List<MapPosition> ss))
                    {
                        foreach (MapPosition s in ss)
                        {
                            if (Mathf.Abs(s.Position.x - x) < 0.5f && Mathf.Abs(s.Position.z - z) < 0.5f) c = 'S';
                        }
                    }

                    if (byType.TryGetValue(MapPointType.OilDispenser, out List<MapPosition> ps))
                    {
                        foreach (MapPosition s in ps)
                        {
                            if (Mathf.Abs(s.Position.x - x) < 0.5f && Mathf.Abs(s.Position.z - z) < 0.5f) c = 'P';
                        }
                    }

                    row.Append(c);
                }

                sb.AppendLine(row.ToString());
            }

            // 지나가는 차의 길. 도로(서쪽)를 따라가는지 주유소 부지(동쪽)를 가로지르는지 꺾는 점으로 본다.
            sb.AppendLine($"  car spawn→exit {PathInfo(from, exit.position, carAgent)}");
            var through = new NavMeshPath();
            if (Sample(exit.position, carAgent, out Vector3 exitOnMesh) &&
                NavMesh.CalculatePath(from, exitOnMesh, new NavMeshQueryFilter { agentTypeID = carAgent, areaMask = NavMesh.AllAreas }, through))
            {
                var corners = new List<string>();
                foreach (Vector3 c in through.corners)
                {
                    corners.Add($"({c.x:0},{c.z:0})");
                }

                sb.AppendLine($"    corners x,z: {string.Join(" ", corners)}");
            }

            foreach (MapPointType type in new[] { MapPointType.ParkingSlot, MapPointType.Entrance, MapPointType.Patrol })
            {
                if (!byType.TryGetValue(type, out List<MapPosition> list))
                {
                    continue;
                }

                foreach (MapPosition p in list)
                {
                    sb.AppendLine($"  car spawn→{type} {p.name,-18} {PathInfo(from, p.Position, carAgent)}");
                    sb.AppendLine($"    corners x,z: {Corners(from, p.Position, carAgent)}");
                    if (Sample(p.Position, carAgent, out Vector3 at))
                    {
                        sb.AppendLine($"  car {type} {p.name,-18}→exit(snapped 20m) {PathInfo(at, exit.position, carAgent, 20f)}");
                        sb.AppendLine($"    corners x,z: {Corners(at, exit.position, carAgent)}");
                    }
                }
            }
        }

        /// <summary>넓은 지도(2m 칸). 바닥: '='=도로 콜라이더 위 차 NavMesh, '#'=도로 밖 차 NavMesh, '.'=차 못 감.
        /// 겹쳐 그리기: 'o'=지나가는 차 길(스폰→출구), 'v'=방문 차 들어오는 길(스폰→자리), '^'=나가는 길(자리→출구),
        /// S=스폰, X=출구, E=입구, P=주차 자리.</summary>
        private static void ReportTrafficMap(StringBuilder sb, Transform spawn, Transform exit, Vector3 from, int carAgent, Dictionary<MapPointType, List<MapPosition>> byType)
        {
            const float cell = 2f, minX = -10f, maxX = 60f, minZ = -40f, maxZ = 132f;
            int w = Mathf.CeilToInt((maxX - minX) / cell) + 1, h = Mathf.CeilToInt((maxZ - minZ) / cell) + 1;
            var grid = new char[h, w];
            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    var p = new Vector3(minX + c * cell, 0.1f, maxZ - r * cell);
                    bool mesh = Sample(p, carAgent, out Vector3 hit, 0.7f) && Vector2.Distance(new Vector2(hit.x, hit.z), new Vector2(p.x, p.z)) < 0.7f;
                    bool road = Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out RaycastHit rh, 60f) && rh.collider.name.ToLower().Contains("road");
                    grid[r, c] = mesh ? (road ? '=' : '#') : '.';
                }
            }

            void Put(Vector3 p, char ch)
            {
                int c = Mathf.RoundToInt((p.x - minX) / cell), r = Mathf.RoundToInt((maxZ - p.z) / cell);
                if (r >= 0 && r < h && c >= 0 && c < w) grid[r, c] = ch;
            }

            void Draw(Vector3 a, Vector3 b, char ch)
            {
                var path = new NavMeshPath();
                if (!Sample(b, carAgent, out Vector3 end, 20f) || !Sample(a, carAgent, out Vector3 start, 20f) ||
                    !NavMesh.CalculatePath(start, end, new NavMeshQueryFilter { agentTypeID = carAgent, areaMask = NavMesh.AllAreas }, path))
                {
                    return;
                }

                for (int i = 1; i < path.corners.Length; i++)
                {
                    float len = Vector3.Distance(path.corners[i - 1], path.corners[i]);
                    for (float d = 0f; d <= len; d += 1f)
                    {
                        Put(Vector3.Lerp(path.corners[i - 1], path.corners[i], d / Mathf.Max(len, 0.01f)), ch);
                    }
                }
            }

            Draw(spawn.position, exit.position, 'o');
            if (byType.TryGetValue(MapPointType.ParkingSlot, out List<MapPosition> slots))
            {
                foreach (MapPosition s in slots)
                {
                    Draw(spawn.position, s.Position, 'v');
                    Draw(s.Position, exit.position, '^');
                }

                foreach (MapPosition s in slots) Put(s.Position, 'P');
            }

            if (byType.TryGetValue(MapPointType.Entrance, out List<MapPosition> entrances))
            {
                foreach (MapPosition e in entrances) Put(e.Position, 'E');
            }

            Put(spawn.position, 'S');
            Put(exit.position, 'X');

            sb.AppendLine($"  traffic map x {minX}→{maxX}, z {maxZ}→{minZ}, {cell}m cells ('='road '#'offroad mesh '.'none o pass v in ^ out):");
            for (int r = 0; r < h; r++)
            {
                var row = new StringBuilder($"    {maxZ - r * cell,5:0} ");
                for (int c = 0; c < w; c++) row.Append(grid[r, c]);
                sb.AppendLine(row.ToString());
            }
        }

        private static string Corners(Vector3 from, Vector3 to, int agentType)
        {
            var path = new NavMeshPath();
            if (!Sample(to, agentType, out Vector3 end, 20f) ||
                !NavMesh.CalculatePath(from, end, new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas }, path))
            {
                return "-";
            }

            var corners = new List<string>();
            foreach (Vector3 c in path.corners)
            {
                corners.Add($"({c.x:0.0},{c.z:0.0})");
            }

            return string.Join(" ", corners);
        }

        private static int FindCustomerAgentType(out string source)
        {
            foreach (string guid in AssetDatabase.FindAssets("Customer t:Prefab", new[] { "Assets/_Works/CJW/Prefabs/Customers" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                NavMeshAgent agent = prefab != null ? prefab.GetComponentInChildren<NavMeshAgent>(true) : null;
                if (agent != null)
                {
                    source = prefab.name;
                    return agent.agentTypeID;
                }
            }

            source = "default";
            return 0;
        }

        private static bool Sample(Vector3 pos, int agentType, out Vector3 hitPos, float radius = SampleRadius)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, radius, filter))
            {
                hitPos = hit.position;
                return true;
            }

            hitPos = pos;
            return false;
        }

        private static string PathInfo(Vector3 from, Vector3 to, int agentType, float targetRadius = SampleRadius)
        {
            if (!Sample(to, agentType, out Vector3 end, targetRadius))
            {
                return "target off navmesh";
            }

            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, end, filter, path))
            {
                return "NO PATH";
            }

            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            string gap = path.status == NavMeshPathStatus.PathPartial && path.corners.Length > 0
                ? $" stops {Vector3.Distance(path.corners[path.corners.Length - 1], end):0.0}m short"
                : "";
            return $"{path.status} len={length:0.0}m exact={(Vector3.Distance(to, end) < 0.3f ? "yes" : $"no({Vector3.Distance(to, end):0.00})")}{gap}";
        }

        private static string DescribeRef(SerializedProperty prop, int maxFields)
        {
            if (prop == null)
            {
                return "null";
            }

            if (prop.propertyType != SerializedPropertyType.ManagedReference)
            {
                return Value(prop);
            }

            if (string.IsNullOrEmpty(prop.managedReferenceFullTypename))
            {
                return "null";
            }

            string type = prop.managedReferenceFullTypename;
            type = type.Substring(type.LastIndexOf('.') + 1);

            var fields = new List<string>();
            SerializedProperty it = prop.Copy();
            SerializedProperty end = prop.GetEndProperty();
            bool enter = true;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                enter = false;
                if (it.propertyType == SerializedPropertyType.ManagedReference)
                {
                    string nested = DescribeRef(it, 3);
                    if (nested != "null")
                    {
                        fields.Add($"{it.name}=<{nested}>");
                    }

                    continue;
                }

                string v = Value(it);
                if (v.Length > 0 && v != "None" && v != "0" && v != "False" && v != "(0.0, 0.0, 0.0)" && !(it.isArray && it.arraySize == 0 && it.propertyType != SerializedPropertyType.String))
                {
                    fields.Add($"{it.name}={v}");
                }
            }

            return $"{type} {{{string.Join(", ", fields)}}}";
        }

        private static string Value(SerializedProperty p)
        {
            if (p == null)
            {
                return "?";
            }

            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.intValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.Float: return p.floatValue.ToString("0.##");
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Enum: return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumNames.Length ? p.enumNames[p.enumValueIndex] : p.intValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    if (p.objectReferenceValue == null) return "NULL";
                    return p.objectReferenceValue is Transform t ? $"{t.name}{Fmt(t.position)}" : p.objectReferenceValue.name;
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString();
                case SerializedPropertyType.Generic when p.isArray:
                    var items = new List<string>();
                    for (int i = 0; i < p.arraySize && i < 8; i++)
                    {
                        items.Add(Value(p.GetArrayElementAtIndex(i)));
                    }

                    return $"[{string.Join(",", items)}]";
                default: return "";
            }
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }

        private static string Fmt(Vector3 v) => $"({v.x:0.0},{v.y:0.0},{v.z:0.0})";
    }
}
