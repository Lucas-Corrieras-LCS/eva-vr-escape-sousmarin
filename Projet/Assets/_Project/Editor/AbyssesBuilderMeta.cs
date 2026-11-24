using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using Abysses.Interaction;
using Abysses.Level;

namespace Abysses.EditorTools
{
    /// <summary>
    /// Générateur, méta-énigme de fin d'étage : un verrou de cabine par module (mur du voisin, x local = 1 m),
    /// avec l'écran des fragments confiés à ce module et un pavé numérique. Voir <see cref="FloorLock"/>.
    /// </summary>
    public static partial class AbyssesBuilder
    {
        const float FloorLockWallX = 1.0f;

        /// <summary>Crée le verrou de l'étage ; <paramref name="groups"/> = une énigme par fragment, dans l'ordre du code.</summary>
        static PuzzleObjective BuildFloorLock(Mats m, Transform parent, Module[] modules, int floor, List<PuzzleObjective[]> groups, int shift)
        {
            var lockGo = new GameObject("VerrouCabine_Etage" + floor);
            lockGo.transform.SetParent(parent, false);
            lockGo.AddComponent<NetworkObject>();
            PuzzleObjective objective = Objective(lockGo, floor, -1, "Code de la cabine", "Cabin code", "floorlock");
            var floorLock = lockGo.AddComponent<FloorLock>();
            var so = new UnityEditor.SerializedObject(floorLock);
            so.FindProperty("objective").objectReferenceValue = objective;
            so.FindProperty("shift").intValue = shift;
            SerializedArray(so.FindProperty("groups"), groups.Count, (element, i) => SetArray(element.FindPropertyRelative("members"), groups[i]));
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (Module mod in modules)
            {
                var root = new GameObject("Verrou_Q" + mod.Q);
                root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(mod.P(FloorLockWallX, 1.3f, -mod.Vz * 3.62f), mod.Facing(0f, -mod.Vz));
                Transform t = root.transform;
                var panel = root.AddComponent<FloorLockPanel>();
                DeviceMount(m, t, new Vector2(0.7f, 0.9f), 1.3f);
                Prim(PrimitiveType.Cube, "Boitier", t, new Vector3(0f, 0f, -0.02f), Quaternion.identity, new Vector3(0.62f, 0.82f, 0.06f), m.Panel, false);
                Label(t, "VERROU DE LA CABINE", new Vector3(-0.03f, 0.36f, -0.055f), 0.34f, null, "CABIN LOCK");
                Renderer lamp = Prim(PrimitiveType.Sphere, "Status", t, new Vector3(0.25f, 0.36f, -0.06f), Quaternion.identity, Vector3.one * 0.045f, m.StatusIdle, false).GetComponent<Renderer>();

                Prim(PrimitiveType.Cube, "Ecran", t, new Vector3(0f, 0.2f, -0.052f), Quaternion.identity, new Vector3(0.54f, 0.24f, 0.004f), m.SlotEmpty, false);
                TextMeshPro fragments = Text(t, "Fragments", new Vector3(0f, 0.2f, -0.056f), Quaternion.identity, new Vector2(0.51f, 0.22f), 0.16f, new Color(0.55f, 1f, 0.8f), true);
                fragments.enableAutoSizing = true;
                fragments.fontSizeMin = 0.1f;
                fragments.fontSizeMax = 0.17f;
                Private(root, mod.Q, fragments.GetComponent<Renderer>());

                Prim(PrimitiveType.Cube, "Saisie", t, new Vector3(0f, 0.03f, -0.052f), Quaternion.identity, new Vector3(0.32f, 0.07f, 0.004f), m.SlotEmpty, false);
                TextMeshPro entry = Text(t, "Code", new Vector3(0f, 0.03f, -0.056f), Quaternion.identity, new Vector2(0.3f, 0.07f), 0.5f, new Color(1f, 0.85f, 0.5f), false);
                entry.text = "_ _ _";

                var localOnly = new List<Behaviour>();
                string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" };
                int[] indices = { 1, 2, 3, 4, 5, 6, 7, 8, 9, FloorLockPanel.ClearIndex, 0, FloorLockPanel.ValidateIndex };
                for (int k = 0; k < keys.Length; k++)
                {
                    float x = -0.11f + (k % 3) * 0.11f;
                    float y = -0.07f - (k / 3) * 0.08f;
                    Material cap = keys[k] == "OK" ? m.EmitCyan : keys[k] == "C" ? m.PipeRed : m.Key;
                    PokeButtonObject(m, t, "Touche_" + keys[k], new Vector3(x, y, -0.03f), new Vector2(0.085f, 0.06f), cap, panel, indices[k], null, null, localOnly);
                    TextMeshPro label = Text(t, "Chiffre_" + keys[k], new Vector3(x, y, -0.072f), Quaternion.identity, new Vector2(0.08f, 0.06f), keys[k].Length > 1 ? 0.3f : 0.4f,
                        keys[k] == "OK" ? new Color(0.02f, 0.05f, 0.06f) : Color.white, false);
                    label.text = keys[k];
                    label.fontStyle = FontStyles.Bold;
                }
                LocalOnly(root, mod.Q, localOnly);

                SetFields(panel, ("floorLock", floorLock), ("quadrant", mod.Q), ("fragmentsText", fragments), ("entryText", entry),
                    ("statusLamp", lamp), ("lampIdle", m.StatusIdle), ("lampOk", m.StatusOk), ("lampError", m.StatusError),
                    ("errorSound", Sfx(root, fx.Error, 0.8f)), ("successSound", Sfx(root, fx.Success, 0.9f)));
            }
            return objective;
        }

        static void SerializedArray(UnityEditor.SerializedProperty array, int count, System.Action<UnityEditor.SerializedProperty, int> fill)
        {
            array.arraySize = count;
            for (int i = 0; i < count; i++) fill(array.GetArrayElementAtIndex(i), i);
        }
    }
}
