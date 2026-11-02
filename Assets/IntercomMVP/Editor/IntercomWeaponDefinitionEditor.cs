#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace IntercomMvp.Editor
{
    [CustomEditor(typeof(IntercomWeaponDefinition))]
    public sealed class IntercomWeaponDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var weapon=(IntercomWeaponDefinition)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Баланс: все пули попали в одну цель",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Пуль в залпе",weapon.ProjectilesPerVolley.ToString());
            EditorGUILayout.LabelField("Залпов в минуту",(60d/weapon.Interval).ToString("0.##"));
            EditorGUILayout.LabelField("Урон/с без перезарядки",weapon.IdealDps.ToString("0.##"));
            EditorGUILayout.LabelField("Урон/с с перезарядкой",weapon.SustainedDps.ToString("0.##"));
            EditorGUILayout.LabelField("Патронов в магазине",weapon.MagazineCapacity.ToString());
            EditorGUILayout.LabelField("Залпов до поражения",weapon.IdealVolleys.ToString());
            EditorGUILayout.LabelField("Идеальный TTK",weapon.IdealTtk.ToString("0.###")+" с");
            EditorGUILayout.HelpBox("TTK считается от первого выстрела. Первый залп — в момент 0. Не учитывает поднятие рук, полёт пули, промахи и препятствия. Учитывает полный магазин и автоматическую перезарядку, начинающуюся после последнего залпа. Запас вне магазина бесконечный. Reference Target Health меняет только расчёт, здоровье цели настраивается отдельно.",MessageType.Info);
            if(weapon.poseProfile!=null)
            {
                EditorGUILayout.LabelField("Связанные анимации",EditorStyles.boldLabel);
                using(new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Поднятие",weapon.poseProfile.raiseWeapons,typeof(AnimationClip),false);
                    EditorGUILayout.ObjectField("Отдача / Shoot",weapon.poseProfile.shoot,typeof(AnimationClip),false);
                    EditorGUILayout.ObjectField("Опускание",weapon.poseProfile.lowerWeapons,typeof(AnimationClip),false);
                }
                if(GUILayout.Button("Открыть профиль анимаций")) Selection.activeObject=weapon.poseProfile;
            }
            EditorGUILayout.HelpBox("В игре источник настройки — поле Weapon у IntercomSimpleShooter. Баланс и размер эффектов читаются из этого файла; материалы пула назначаются при запуске. Старые поля компонентов используются только без Weapon.",MessageType.None);
        }
    }
}
#endif
