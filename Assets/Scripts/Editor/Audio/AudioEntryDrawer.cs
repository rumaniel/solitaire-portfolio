using Data.Audio;
using UnityEditor;
using UnityEngine;
using AudioType = Data.Audio.AudioType;

namespace Editor.Audio
{
    [CustomPropertyDrawer(typeof(AudioEntry))]
    public class AudioEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            var audioTypeProp = property.FindPropertyRelative("audioType");
            var isSfx = (AudioType)audioTypeProp.enumValueIndex == AudioType.Sfx;

            // Type
            y = DrawField(position, y, audioTypeProp);

            // Clip
            y = DrawField(position, y, property.FindPropertyRelative("clipReference"));
            y = DrawField(position, y, property.FindPropertyRelative("extraClips"));
            y = DrawField(position, y, property.FindPropertyRelative("clipSelectionMode"));

            // Playback
            y = DrawField(position, y, property.FindPropertyRelative("delay"));
            y = DrawField(position, y, property.FindPropertyRelative("volume"));

            // Loop: Music only
            if (!isSfx)
                y = DrawField(position, y, property.FindPropertyRelative("loop"));

            // Fade
            y = DrawField(position, y, property.FindPropertyRelative("fadeInDuration"));
            y = DrawField(position, y, property.FindPropertyRelative("fadeInCurve"));
            y = DrawField(position, y, property.FindPropertyRelative("fadeOutDuration"));
            y = DrawField(position, y, property.FindPropertyRelative("fadeOutCurve"));

            // Duplicate Policy: SFX only
            if (isSfx)
            {
                var policyProp = property.FindPropertyRelative("duplicatePolicy");
                y = DrawField(position, y, policyProp);

                var policyValue = (DuplicatePlayPolicy)policyProp.enumValueIndex;
                if (policyValue != DuplicatePlayPolicy.All)
                    y = DrawField(position, y, property.FindPropertyRelative("maxInstances"));
            }

            // Mixer
            y = DrawField(position, y, property.FindPropertyRelative("mixerGroup"));

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            // Foldout line
            float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            var audioTypeProp = property.FindPropertyRelative("audioType");
            var isSfx = (AudioType)audioTypeProp.enumValueIndex == AudioType.Sfx;

            // Same fields as OnGUI — use actual property heights
            height += FieldHeight(property, "audioType");
            height += FieldHeight(property, "clipReference");
            height += FieldHeight(property, "extraClips");
            height += FieldHeight(property, "clipSelectionMode");
            height += FieldHeight(property, "delay");
            height += FieldHeight(property, "volume");

            if (!isSfx)
                height += FieldHeight(property, "loop");

            height += FieldHeight(property, "fadeInDuration");
            height += FieldHeight(property, "fadeInCurve");
            height += FieldHeight(property, "fadeOutDuration");
            height += FieldHeight(property, "fadeOutCurve");

            if (isSfx)
            {
                height += FieldHeight(property, "duplicatePolicy");
                var policy = (DuplicatePlayPolicy)property.FindPropertyRelative("duplicatePolicy").enumValueIndex;
                if (policy != DuplicatePlayPolicy.All)
                    height += FieldHeight(property, "maxInstances");
            }

            height += FieldHeight(property, "mixerGroup");

            return height;
        }

        private static float FieldHeight(SerializedProperty parent, string name)
        {
            var prop = parent.FindPropertyRelative(name);
            return EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        private static float DrawField(Rect position, float y, SerializedProperty prop)
        {
            float h = EditorGUI.GetPropertyHeight(prop, true);
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, h), prop, true);
            return y + h + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
