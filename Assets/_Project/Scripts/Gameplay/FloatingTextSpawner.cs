using System.Collections;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public enum FloatingTextKind
    {
        EnemyDamage,
        PlayerDamage,
        Heal,
        Alert
    }

    /// <summary>
    /// 피격/회복 수치를 머리 위로 띄우는 플로팅 텍스트. (프로토타입)
    /// 씬 셋업 없이 코드로 생성되며, 다른 임시 연출과 같은 수동 코루틴 애니메이션을 쓴다.
    /// </summary>
    public class FloatingTextSpawner : MonoBehaviour
    {
        private const float RiseDistance = 0.6f;
        private const float Duration = 0.7f;
        private const int SortingOrder = 32000;

        private Font _font;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public void ShowDamage(Vector3 worldPos, int amount, FloatingTextKind kind) =>
            Show(worldPos, kind == FloatingTextKind.Heal ? $"+{amount}" : $"-{amount}", kind);

        public void Show(Vector3 worldPos, string text, FloatingTextKind kind)
        {
            if (!Application.isPlaying) return;

            var instance = new GameObject($"Floating Text {text}");
            instance.transform.SetParent(transform, false);
            // 같은 칸 연타 시 겹치지 않게 약간 흩뿌린다.
            instance.transform.position = worldPos + Vector3.up * 0.7f +
                                          Vector3.right * Random.Range(-0.12f, 0.12f);

            var textMesh = instance.AddComponent<TextMesh>();
            textMesh.font = _font;
            textMesh.text = text;
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.055f;
            textMesh.fontStyle = FontStyle.Bold;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = ColorFor(kind);

            var renderer = instance.GetComponent<MeshRenderer>();
            renderer.material = _font.material;
            renderer.sortingOrder = SortingOrder;

            StartCoroutine(Animate(instance, textMesh));
        }

        private static Color ColorFor(FloatingTextKind kind)
        {
            switch (kind)
            {
                case FloatingTextKind.PlayerDamage: return new Color32(255, 96, 80, 255);
                case FloatingTextKind.Heal: return new Color32(112, 228, 140, 255);
                case FloatingTextKind.Alert: return new Color32(255, 224, 96, 255);
                default: return new Color32(255, 208, 112, 255);
            }
        }

        private static IEnumerator Animate(GameObject instance, TextMesh textMesh)
        {
            Vector3 start = instance.transform.position;
            Color baseColor = textMesh.color;
            float elapsed = 0f;
            while (elapsed < Duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Duration);
                instance.transform.position = start + Vector3.up * (RiseDistance * t);
                baseColor.a = 1f - t * t;
                textMesh.color = baseColor;
                yield return null;
            }
            Destroy(instance);
        }
    }
}
