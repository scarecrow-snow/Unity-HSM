#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace HSM.Editor
{
    public class StateMachineViewer : EditorWindow
    {
        private Vector2 scrollPosition;
        private IStateMachineProvider selectedProvider;
        private GameObject selectedGameObject;

        float contentScale = 1.0f;
        float minContentScale = 0.5f;
        float maxContentScale = 2.0f;
        float indentSize = 20f;
        int depth = 0;

        // Colors
        Color ActiveColor = Color.green;
        Color InactiveColor = new Color(0.2f, 0.2f, 0.2f);

        GUIStyle labelStyle;
        GUIStyle foldoutStyle;

        [MenuItem("HSM/State Machine Viewer")]
        public static void ShowWindow()
        {
            GetWindow<StateMachineViewer>("HSM Viewer");
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += OnUpdate;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= OnUpdate;
        }

        void OnSelectionChanged()
        {
            if (Selection.activeGameObject == null)
                return;

            var provider = Selection.activeGameObject.GetComponent<IStateMachineProvider>();
            if (provider == null)
                provider = Selection.activeGameObject.GetComponentInChildren<IStateMachineProvider>();

            if (provider != null)
            {
                selectedProvider = provider;
                selectedGameObject = Selection.activeGameObject;
            }
        }

        private void OnUpdate()
        {
            if (!IsAlive(selectedProvider) || !IsAlive(selectedGameObject))
            {
                selectedProvider = null;
                selectedGameObject = null;
            }

            if (selectedProvider == null)
                OnSelectionChanged();

            Repaint();
        }

        private void OnGUI()
        {
            // Content scale slider
            GUILayout.BeginHorizontal();
            contentScale = EditorGUILayout.Slider("Content Scale", contentScale, minContentScale, maxContentScale);
            GUILayout.EndHorizontal();

            float scaledLineHeight = EditorGUIUtility.singleLineHeight * contentScale;

            labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = Mathf.RoundToInt(EditorStyles.label.fontSize * contentScale)
            };

            foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = Mathf.RoundToInt(EditorStyles.foldout.fontSize * contentScale)
            };

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            depth = 0;

            if (IsAlive(selectedProvider) && selectedProvider.Machine != null)
            {
                DrawHeader(selectedProvider.Machine);
                DrawState(selectedProvider.Machine.Root);
            }
            else
            {
                EditorGUILayout.LabelField("Select a GameObject with IStateMachineProvider component.", labelStyle);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawHeader(StateMachine machine)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(EditorGUIUtility.singleLineHeight * contentScale));
            EditorGUILayout.LabelField($"State Machine: {selectedGameObject?.name ?? "Unknown"}", labelStyle);

            if (Application.isPlaying)
            {
                State leaf = machine.Root.Leaf();
                EditorGUILayout.LabelField($"Active: {leaf.GetType().Name}", labelStyle);
            }

            GUILayout.EndHorizontal();
        }

        void DrawState(State state)
        {
            if (state == null)
                return;

            // Determine if state is on active path
            bool isActive = IsOnActivePath(state);

            Color oldColor = GUI.color;
            GUI.color = GetStateColor(state, isActive);

            BeginState();

            // Foldout with state name
            state.ViewParameters.ShowFoldout = EditorGUILayout.Foldout(
                state.ViewParameters.ShowFoldout,
                GetStateName(state),
                foldoutStyle);

            EndState(isActive);

            // Draw children
            if (state.ViewParameters.ShowFoldout)
            {
                depth++;

                // Draw activities (if any)
                if (state.Activities.Count > 0)
                {
                    DrawActivities(state);
                }

                // Draw active child (if any)
                if (state.ActiveChild != null)
                {
                    DrawState(state.ActiveChild);
                }

                depth--;
            }

            GUI.color = oldColor;
        }

        void BeginState()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(EditorGUIUtility.singleLineHeight * contentScale));
            GUILayout.Space(indentSize * contentScale * (depth + 1));
        }

        void EndState(bool isActive)
        {
            GUILayout.EndHorizontal();

            float y = GUILayoutUtility.GetLastRect().y;

            // Draw indicator box
            Color boxColor = isActive ? ActiveColor : InactiveColor;
            Rect rect = new Rect(0, y, indentSize * contentScale,
                EditorGUIUtility.singleLineHeight * contentScale);
            EditorGUI.DrawRect(rect, boxColor);

            // Draw hierarchy arrow
            if (depth > 0)
            {
                DrawHierarchyArrow(y);
            }
        }

        void DrawHierarchyArrow(float y)
        {
            float arrowThickness = 1f * contentScale;
            float arrowLength = (indentSize + arrowThickness) * contentScale / 2f;
            float x = (indentSize * depth + indentSize * 0.5f - arrowThickness / 2f) * contentScale;

            Rect rect = new Rect(x, y, arrowThickness, arrowLength);
            EditorGUI.DrawRect(rect, GUI.skin.label.normal.textColor);

            y += indentSize * 0.5f * contentScale - arrowThickness;
            rect = new Rect(x, y, arrowLength - arrowThickness, arrowThickness);
            EditorGUI.DrawRect(rect, GUI.skin.label.normal.textColor);
        }

        bool IsAlive(object target)
        {
            if (target is UnityEngine.Object unityObj)
            {
                return unityObj != null;
            }

            return target != null;
        }

        bool IsOnActivePath(State state)
        {
            if (!Application.isPlaying || !IsAlive(selectedProvider) || selectedProvider.Machine == null)
                return false;

            State leaf = selectedProvider.Machine.Root.Leaf();
            foreach (var s in leaf.PathToRoot())
            {
                if (s == state)
                    return true;
            }
            return false;
        }

        Color GetStateColor(State state, bool isActive)
        {
            return isActive ? ActiveColor : GUI.skin.label.normal.textColor;
        }

        string GetStateName(State state)
        {
            string name = state.GetType().Name;
            if (state.ActiveChild != null)
            {
                name += $" → {state.ActiveChild.GetType().Name}";
            }
            return name;
        }

        void DrawActivities(State state)
        {
            foreach (var activity in state.Activities)
            {
                DrawActivity(activity);
            }
        }

        void DrawActivity(IActivity activity, int nestedLevel = 0)
        {
            BeginState();

            Color oldColor = GUI.color;

            // Color based on activity mode
            Color activityColor = GetActivityColor(activity);
            GUI.color = activityColor;

            string prefix = nestedLevel > 0 ? new string(' ', nestedLevel * 2) + "└─ " : "[Activity] ";
            EditorGUILayout.LabelField($"{prefix}{activity.GetType().Name} ({activity.Mode})", labelStyle);

            GUI.color = oldColor;

            EndState(false);

            // If it's a SequentialActivityGroup, draw its nested activities
            if (activity is SequentialActivityGroup group)
            {
                depth++;
                foreach (var nestedActivity in group.Activities)
                {
                    DrawActivity(nestedActivity, nestedLevel + 1);
                }
                depth--;
            }
        }

        Color GetActivityColor(IActivity activity)
        {
            switch (activity.Mode)
            {
                case ActivityMode.Active:
                    return new Color(0.5f, 1f, 0.5f); // Bright green
                case ActivityMode.Activating:
                    return new Color(0.5f, 0.8f, 1f); // Cyan
                case ActivityMode.Deactivating:
                    return new Color(1f, 0.7f, 0.5f); // Orange
                case ActivityMode.Inactive:
                default:
                    return new Color(0.7f, 0.7f, 0.7f); // Gray
            }
        }
    }
}

#endif
