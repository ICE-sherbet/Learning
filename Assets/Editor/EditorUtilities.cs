using UnityEditor;
using UnityEngine;

namespace QBuild.StageEditor
{
    public static class EditorUtilities
    {
        public static void ChangeStageDataProperty(SerializedObject so, string variable, object value)
        {
            var property = so.FindProperty(variable);
            switch (value)
            {
                case int intValue:
                    property.intValue = intValue;
                    break;
                case float floatValue:
                    property.floatValue = floatValue;
                    break;
                case bool boolValue:
                    property.boolValue = boolValue;
                    break;
                case string stringValue:
                    property.stringValue = stringValue;
                    break;
                case Texture2D texture2DValue:
                    property.objectReferenceValue = texture2DValue;
                    break;
                case Vector3Int vector3IntValue:
                    property.vector3IntValue = vector3IntValue;
                    break;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }
}