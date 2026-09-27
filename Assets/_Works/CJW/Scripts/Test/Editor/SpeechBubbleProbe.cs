using System.IO;
using System.Text;
using _Works.JYG._Scripts.UI.SpeechBubble;
using DevLib.ObjectPool.Runtime;
using Resources.DataBase.Human_Data;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>말풍선이 안 보이는 원인을 찾는 디버그용 도구. 플레이 중에 카메라 앞에 말풍선을 하나 띄우고 카메라·캔버스·그래픽 상태를 적는다.
    /// 결과는 Temp/CustomerSim/bubble.txt. 확인이 끝나면 지워도 된다.</summary>
    public static class SpeechBubbleProbe
    {
        [MenuItem("Tools/CJW/Probe Speech Bubble")]
        private static void Probe()
        {
            var sb = new StringBuilder();

            foreach (Camera cam in Camera.allCameras)
            {
                sb.AppendLine($"cam {cam.name} depth={cam.depth} UI레이어={(cam.cullingMask & (1 << 5)) != 0} mask={cam.cullingMask} pos={cam.transform.position} target={(cam.targetTexture != null)}");
            }

            Camera main = Camera.main;
            sb.AppendLine($"main={(main != null ? main.name : "null")}");

            foreach (SpeechBubble b in Object.FindObjectsByType<SpeechBubble>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b.gameObject.activeInHierarchy)
                {
                    Dump(sb, "씬에 켜진 말풍선", b, main);
                }
            }

            if (EditorApplication.isPlaying)
            {
                var pm = AssetDatabase.LoadAssetAtPath<PoolManagerSO>(AssetDatabase.GUIDToAssetPath("c5baccba6f7390e45915d59f83b4381e"));
                var item = AssetDatabase.LoadAssetAtPath<PoolItemSO>(AssetDatabase.GUIDToAssetPath("2a00163c0ed52214a828e00935dbf2e5"));
                sb.AppendLine($"pm={pm?.name} item={item?.name} 목록에 있음={pm != null && pm.itemList.Contains(item)}");

                SpeechBubble b = pm?.Pop<SpeechBubble>(item);
                if (b == null)
                {
                    sb.AppendLine("Pop 결과 null");
                }
                else
                {
                    if (main != null)
                    {
                        b.transform.position = main.transform.position + main.transform.forward * 3f;
                    }

                    b.InitializeBubble(HumanType.Good, 1);
                    Dump(sb, "카메라 앞에 띄운 말풍선", b, main);
                }
            }

            Directory.CreateDirectory("Temp/CustomerSim");
            File.WriteAllText("Temp/CustomerSim/bubble.txt", sb.ToString());
            Debug.Log("[SpeechBubbleProbe] Temp/CustomerSim/bubble.txt");
        }

        /// <summary>손님 데이터마다 SpeechBubble이 (HumanType, CustomerType)으로 찾게 될 DB 행을 적는다. 결과는 Temp/CustomerSim/bubble_db.txt.</summary>
        [MenuItem("Tools/CJW/Dump Speech Lines")]
        private static void DumpLines()
        {
            var sb = new StringBuilder();
            var db = UnityEngine.Resources.Load<HumanDB>("DataBase/Human Data/HumanDB");

            foreach (string guid in AssetDatabase.FindAssets("t:CustomerDataSO"))
            {
                var data = AssetDatabase.LoadAssetAtPath<_Works.CJW.Scripts.Customers.Data.CustomerDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                GameObject prefab = data.PoolItem != null ? data.PoolItem.prefab : null;
                var customer = prefab != null ? prefab.GetComponent<_Works.CJW.Scripts.Customers.AbstractCustomer>() : null;
                if (customer == null)
                {
                    sb.AppendLine($"{data.name}: 프리팹/손님 없음");
                    continue;
                }

                HumanType human = customer.HumanType;
                int index = (int)data.CustomerType;
                HumanData exact = null, first = null;
                foreach (HumanData row in db.Sheet1)
                {
                    if (row.type != human) continue;
                    first ??= row;
                    if (row.index == index) exact = row;
                }

                string speech = prefab.GetComponentInChildren<_Works.CJW.Scripts.Customers.Visit.CustomerFSM.CustomerFSMModule>(true) is { } fsm
                                && EditorJsonUtility.ToJson(fsm).Contains("bubbleItem") ? "말풍선O" : "말풍선X";
                HumanData used = exact ?? first;
                sb.AppendLine($"{data.name}\t{data.CustomerType}({index})\t{human}\t{speech}\t" +
                              (used == null ? "대사 없음 → 바로 닫힘" : $"{(exact != null ? "일치" : "없음→첫 행")} DB {used.index}: {used.contents1}"));
            }

            Directory.CreateDirectory("Temp/CustomerSim");
            File.WriteAllText("Temp/CustomerSim/bubble_db.txt", sb.ToString());
        }

        private static void Dump(StringBuilder sb, string title, SpeechBubble b, Camera main)
        {
            sb.AppendLine($"== {title}: {b.name}");
            string chain = "";
            for (Transform t = b.transform; t != null; t = t.parent)
            {
                chain += $"{t.name}(active={t.gameObject.activeSelf}, scale={t.localScale}, layer={t.gameObject.layer}) <- ";
            }

            sb.AppendLine($"  계층: {chain}");
            sb.AppendLine($"  pos={b.transform.position} rot={b.transform.rotation.eulerAngles} lossy={b.transform.lossyScale}");

            if (main != null)
            {
                Vector3 vp = main.WorldToViewportPoint(b.transform.position);
                float facing = Vector3.Dot(b.transform.forward, main.transform.forward);
                sb.AppendLine($"  viewport={vp} 거리={Vector3.Distance(main.transform.position, b.transform.position):F2} 카메라와 같은 방향(dot)={facing:F2}");
            }

            foreach (Canvas c in b.GetComponentsInChildren<Canvas>(true))
            {
                sb.AppendLine($"  canvas {c.name} mode={c.renderMode} enabled={c.enabled} worldCam={(c.worldCamera != null ? c.worldCamera.name : "null")} rect={((RectTransform)c.transform).rect.size} sorting={c.sortingLayerName}/{c.sortingOrder}");
            }

            foreach (Graphic g in b.GetComponentsInChildren<Graphic>(true))
            {
                string extra = g is TMP_Text tmp ? $" text='{tmp.text}' font={(tmp.font != null ? tmp.font.name : "null")} size={tmp.fontSize}" : "";
                sb.AppendLine($"  graphic {g.name} {g.GetType().Name} enabled={g.enabled} active={g.gameObject.activeInHierarchy} color={g.color} crAlpha={g.canvasRenderer.GetAlpha()} cull={g.canvasRenderer.cull} rect={g.rectTransform.rect.size}{extra}");
            }

            foreach (CanvasGroup cg in b.GetComponentsInChildren<CanvasGroup>(true))
            {
                sb.AppendLine($"  canvasGroup {cg.name} alpha={cg.alpha}");
            }
        }
    }
}
