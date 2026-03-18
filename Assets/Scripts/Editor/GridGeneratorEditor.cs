using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridGenerator))]
public class GridGeneratorEditor : Editor
{
    void OnEnable()
    {
        var gen = (GridGenerator)target;
        if (gen.randomSeed && gen.previewSeed == 0)
            RollSeed(gen);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var gen = (GridGenerator)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scene Preview", EditorStyles.boldLabel);

        if (gen.randomSeed)
            EditorGUILayout.LabelField($"Preview seed: {gen.previewSeed}", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();

        if (gen.randomSeed && GUILayout.Button("Roll seed"))
            RollSeed(gen);

        if (GUILayout.Button("Refresh"))
            SceneView.RepaintAll();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("Green = kept  |  Red = discarded islands", MessageType.None);
    }

    void RollSeed(GridGenerator gen)
    {
        Undo.RecordObject(gen, "Roll Preview Seed");
        gen.previewSeed = Random.Range(1, 999999);
        EditorUtility.SetDirty(gen);
        SceneView.RepaintAll();
    }
}
