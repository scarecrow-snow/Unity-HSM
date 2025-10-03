#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace HSM.Editor
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    [CanEditMultipleObjects]
    public class StateMachineEditor : UnityEditor.Editor
    {
        bool showData = true;
        IStateMachineProvider provider;

        void OnEnable()
        {
            provider = target as IStateMachineProvider;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (provider == null)
                return;

            EditorGUILayout.Space(15f);

            if (GUILayout.Button("Open HSM Viewer"))
            {
                StateMachineViewer.ShowWindow();
            }

            EditorGUILayout.Space(15f);

            showData = EditorGUILayout.Foldout(showData, "Runtime Data");

            if (showData && Application.isPlaying && provider.Machine != null)
            {
                EditorGUI.indentLevel++;
                DrawRuntimeData(provider.Machine);
                EditorGUI.indentLevel--;
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }

        void DrawRuntimeData(StateMachine machine)
        {
            GUI.enabled = false;

            // Active Path
            State leaf = machine.Root.Leaf();
            EditorGUILayout.TextField("Active Path", State.StatePath(leaf));

            // Activity Executor Status
            EditorGUILayout.Toggle("Is Executing Activities",
                machine.Sequencer.ActivityExecutor.IsExecuting);

            GUI.enabled = true;
        }
    }
}

#endif
