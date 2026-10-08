/// <summary>Version unica del juego para Android e iOS (el numero de build de iOS lo pone Codemagic).</summary>
public static class GameVersion
{
    public const string Name = "0.12.2";
    public const int Code = 22;
    public const string AndroidId = "com.baubagas.minerlings";
    /// <summary>Identificador de iOS por defecto; Codemagic lo pisa con la variable BUNDLE_ID.</summary>
    public const string IosId = "com.baubagas.minerlings";
}
