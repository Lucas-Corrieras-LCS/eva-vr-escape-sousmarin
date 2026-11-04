using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Abysses.Core;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Vérification d'ergonomie (menu Abysses) : chaque objet interactif des étages doit être entre 0,8 et 1,6 m de haut
    /// (ou posé à portée sur un support), et le joueur doit pouvoir l'atteindre sans que sa tête entre dans la marge
    /// de sécurité de 50 cm (consigne C10). Rapport dans docs/ergonomie.md.
    /// </summary>
    public static class AbyssesErgonomics
    {
        const float MinHeight = 0.8f;
        const float MaxHeight = 1.6f;
        /// <summary>Distance entre la main tendue et la tête d'un joueur debout face à l'objet.</summary>
        const float ArmReach = 0.45f;

        static readonly string[] Floors = { "Etage1_Habitat", "Etage2_Labo", "Etage3_Coeur" };

        [MenuItem("Abysses/Vérifier l'ergonomie (hauteurs, marge)", priority = 20)]
        public static void Check()
        {
            var report = new StringBuilder();
            report.AppendLine("# Ergonomie des objets interactifs");
            report.AppendLine();
            report.AppendLine("> Généré par *Abysses → Vérifier l'ergonomie*. Règles : hauteur entre 0,8 et 1,6 m ;");
            report.AppendLine("> la tête du joueur (main tendue à 45 cm) reste hors de la marge de sécurité de 50 cm (C10).");
            report.AppendLine();
            report.AppendLine("| Étage | Objet | Hauteur | Tête ↔ bord | Statut |");
            report.AppendLine("|-------|-------|---------|-------------|--------|");
            int problems = 0, total = 0;
            foreach (string floor in Floors)
            {
                Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + floor + ".unity", OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (XRBaseInteractable interactable in root.GetComponentsInChildren<XRBaseInteractable>(true))
                    {
                        Vector3 p = interactable.transform.position;
                        int quadrant = QuadrantOf(p);
                        if (quadrant < 0) continue;
                        total++;
                        // Position de la tête : en retrait de l'objet, vers le centre du module.
                        Vector3 center = PlayArea.GetCenter(quadrant);
                        Vector3 toCenter = Vector3.ProjectOnPlane(center - p, Vector3.up).normalized;
                        Vector3 head = p + toCenter * ArmReach;
                        Rect rect = PlayArea.GetRect(quadrant);
                        float edge = Mathf.Min(Mathf.Min(head.x - rect.xMin, rect.xMax - head.x), Mathf.Min(head.z - rect.yMin, rect.yMax - head.z));
                        bool heightOk = p.y >= MinHeight - 0.05f && p.y <= MaxHeight + 0.05f;
                        bool marginOk = edge >= PlayArea.SafetyMargin;
                        if (!heightOk || !marginOk) problems++;
                        string status = heightOk && marginOk ? "OK" : (!heightOk ? "hauteur" : "") + (!heightOk && !marginOk ? " + " : "") + (!marginOk ? "marge" : "");
                        report.AppendLine($"| {floor} | {interactable.name} (Q{quadrant}) | {p.y.ToString("F2", CultureInfo.InvariantCulture)} m | {edge.ToString("F2", CultureInfo.InvariantCulture)} m | {status} |");
                    }
                }
            }
            report.AppendLine();
            report.AppendLine($"**{total} objets vérifiés, {problems} à revoir.**");
            Directory.CreateDirectory("../docs");
            File.WriteAllText("../docs/ergonomie.md", report.ToString());
            Debug.Log($"[Abysses] Ergonomie : {total} objets, {problems} à revoir → docs/ergonomie.md");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Station.unity", OpenSceneMode.Single);
        }

        static int QuadrantOf(Vector3 p)
        {
            for (int q = 0; q < PlayArea.QuadrantCount; q++)
                if (PlayArea.IsInside(q, p, 0f)) return q;
            return -1;
        }
    }
}
