using UnityEngine;

/// <summary>
/// Marca una pieza que solo existe en una dificultad: un bloque de sobra de la paleta, una
/// ficha o un hueco de otro juego de figuras. En la otra dificultad DifficultyApplier la apaga
/// al cargar la escena, antes de que despierte.
///
/// Las piezas que valen para las dos no llevan nada. En el editor, Tools → Codea → "Ver como
/// básica / intermedia" oculta en la vista de escena las de la otra, para colocarlas sin
/// que se solapen.
///
/// No la actives desde un paso del Director: volvería a aparecer en la dificultad que no
/// le toca. Si una pieza tiene que encenderse a mitad de partida, enciende su padre.
/// </summary>
[DisallowMultipleComponent]
public class DifficultyOnly : MonoBehaviour
{
    [Tooltip("La única dificultad en la que existe esta pieza.")]
    [SerializeField] private Difficulty difficulty = Difficulty.Avanzada;

    public Difficulty Difficulty
    {
        get => difficulty;
        set => difficulty = value;
    }

    /// <summary>
    /// Si el objeto, o alguno de sus padres, pertenece a la otra dificultad. Lo usan los
    /// controladores que recogen sus piezas con GetComponentsInChildren(true), que también
    /// devuelve las apagadas: un hueco de la otra dificultad dejaría el reto sin terminar.
    /// </summary>
    public static bool IsExcluded(GameObject target)
    {
        if (target == null) return false;

        Difficulty current = DifficultyApplier.Current;

        foreach (DifficultyOnly only in target.GetComponentsInParent<DifficultyOnly>(true))
            if (only.difficulty != current) return true;

        return false;
    }
}
