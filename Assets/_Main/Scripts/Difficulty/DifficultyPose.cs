using UnityEngine;

/// <summary>
/// Una pieza que va en otro sitio según la dificultad. Lo aplica DifficultyApplier al cargar la
/// escena, antes de que nada despierte.
///
/// Lo usa el botón Probar de la Sala 3 (09/11/2026): en básica la consola es más baja, porque
/// no hay columna de instrucciones, y el botón sube junto a las fichas de tipo.
/// </summary>
public class DifficultyPose : DifficultyVariant<DifficultyPose.Pose>
{
    [System.Serializable]
    public class Pose
    {
        [Tooltip("Posición local en esta dificultad.")]
        public Vector3 localPosition;

        [Tooltip("Giro local en esta dificultad (ángulos de Euler).")]
        public Vector3 localEuler;
    }

    protected override void Apply(Pose pose)
    {
        transform.localPosition = pose.localPosition;
        transform.localEulerAngles = pose.localEuler;
    }
}
