using Windows.System.UserProfile;

namespace AuraLite;

internal enum Phrase
{
    Controller, Detect, Ready, Detecting, Detected, Unavailable, Lighting, Channel, ChooseChannel,
    Mode, Static, Cycle, Rainbow, Brightness, LedCount, Profiles, ProfileName, Save, Delete, Load,
    Autostart, Language, WindowsDefault, On, Off, Settings,
    SendFailed, ConfigChanged, MissingChannel, InvalidCount, MissingName,
    Saved, Deleted, MissingPath, ErrorDetails, TrayOpen, TrayExit, RestoreLighting, LightingUpdated, StartupProfile, LastProfile
}

internal sealed class UiLanguage
{
    public static readonly string[] Codes = ["auto", "en", "de", "es", "fr", "pt"];
    public static readonly string[] NativeNames = ["", "English", "Deutsch", "Español", "Français", "Português"];

    private static readonly string[][] Catalog =
    [
        ["Controller", "Controller", "Controlador", "Contrôleur", "Controlador"],
        ["Detect controller", "Controller prüfen", "Detectar controlador", "Détecter le contrôleur", "Detetar controlador"],
        ["Ready to read firmware and configuration.", "Bereit zur Firmware- und Konfigurationsabfrage.", "Listo para leer el firmware y la configuración.", "Prêt à lire le micrologiciel et la configuration.", "Pronto para ler o firmware e a configuração."],
        ["Reading firmware and configuration…", "Firmware und Konfiguration werden abgefragt …", "Leyendo el firmware y la configuración…", "Lecture du micrologiciel et de la configuration…", "A ler o firmware e a configuração…"],
        ["Controller detected", "Controller erkannt", "Controlador detectado", "Contrôleur détecté", "Controlador detetado"],
        ["Controller unavailable", "Controller nicht verfügbar", "Controlador no disponible", "Contrôleur indisponible", "Controlador indisponível"],
        ["Lighting", "Beleuchtung", "Iluminación", "Éclairage", "Iluminação"],
        ["ARGB channel", "ARGB-Kanal", "Canal ARGB", "Canal ARGB", "Canal ARGB"],
        ["Select a channel", "Kanal auswählen", "Seleccionar canal", "Sélectionner un canal", "Selecionar canal"],
        ["Effect", "Effekt", "Efecto", "Effet", "Efeito"],
        ["Static", "Statisch", "Estático", "Fixe", "Estático"],
        ["Color cycle", "Farbzyklus", "Ciclo de colores", "Cycle de couleurs", "Ciclo de cores"],
        ["Rainbow · hardware test", "Regenbogen · Hardwaretest", "Arcoíris · prueba de hardware", "Arc-en-ciel · test matériel", "Arco-íris · teste de hardware"],
        ["Brightness (%)", "Helligkeit (%)", "Brillo (%)", "Luminosité (%)", "Brilho (%)"],
        ["LEDs on selected channel", "LEDs am gewählten Ausgang", "LED del canal seleccionado", "LED du canal sélectionné", "LED do canal selecionado"],
        ["Profiles", "Profile", "Perfiles", "Profils", "Perfis"],
        ["Profile name", "Profilname", "Nombre del perfil", "Nom du profil", "Nome do perfil"],
        ["Save", "Speichern", "Guardar", "Enregistrer", "Guardar"],
        ["Delete", "Löschen", "Eliminar", "Supprimer", "Eliminar"],
        ["Load a saved profile", "Gespeichertes Profil laden", "Cargar un perfil guardado", "Charger un profil enregistré", "Carregar um perfil guardado"],
        ["Open AuraLite at Windows sign-in", "AuraLite bei der Windows-Anmeldung öffnen", "Abrir AuraLite al iniciar sesión en Windows", "Ouvrir AuraLite à la connexion Windows", "Abrir o AuraLite ao iniciar sessão no Windows"],
        ["Language", "Sprache", "Idioma", "Langue", "Idioma"],
        ["Windows default", "Windows-Standard", "Predeterminado de Windows", "Par défaut de Windows", "Predefinição do Windows"],
        ["On", "Ein", "Activado", "Activé", "Ativado"],
        ["Off", "Aus", "Desactivado", "Désactivé", "Desativado"],
        ["Settings", "Einstellungen", "Configuración", "Paramètres", "Definições"],
        ["Transfer failed", "Übertragung fehlgeschlagen", "Error de transferencia", "Échec du transfert", "Falha na transferência"],
        ["The configuration changed. Detect the controller again.", "Die Konfiguration hat sich geändert. Controller erneut prüfen.", "La configuración ha cambiado. Detecta de nuevo el controlador.", "La configuration a changé. Détectez à nouveau le contrôleur.", "A configuração mudou. Detete novamente o controlador."],
        ["Select an available ARGB channel.", "Einen verfügbaren ARGB-Kanal auswählen.", "Selecciona un canal ARGB disponible.", "Sélectionnez un canal ARGB disponible.", "Selecione um canal ARGB disponível."],
        ["Enter a whole number of LEDs.", "Die LED-Anzahl muss eine ganze Zahl sein.", "Introduce un número entero de LED.", "Saisissez un nombre entier de LED.", "Introduza um número inteiro de LED."],
        ["Enter a profile name.", "Einen Profilnamen eingeben.", "Introduce un nombre de perfil.", "Saisissez un nom de profil.", "Introduza um nome de perfil."],
        ["Profile saved", "Profil gespeichert", "Perfil guardado", "Profil enregistré", "Perfil guardado"],
        ["Profile deleted", "Profil gelöscht", "Perfil eliminado", "Profil supprimé", "Perfil eliminado"],
        ["The app path could not be found.", "Programmpfad wurde nicht gefunden.", "No se encontró la ruta de la aplicación.", "Le chemin de l’application est introuvable.", "Não foi possível localizar o caminho da aplicação."],
        ["Diagnostics", "Diagnose", "Diagnóstico", "Diagnostic", "Diagnóstico"],
        ["Open AuraLite", "AuraLite öffnen", "Abrir AuraLite", "Ouvrir AuraLite", "Abrir AuraLite"],
        ["Quit", "Beenden", "Salir", "Quitter", "Sair"],
        ["Restore lighting at launch and after sleep", "Beleuchtung beim Start und nach Standby wiederherstellen", "Restaurar iluminación al iniciar y tras la suspensión", "Rétablir l’éclairage au démarrage et après veille", "Restaurar iluminação ao iniciar e após suspensão"],
        ["Lighting updated", "Beleuchtung aktualisiert", "Iluminación actualizada", "Éclairage mis à jour", "Iluminação atualizada"],
        ["Startup profile", "Startprofil", "Perfil de inicio", "Profil au démarrage", "Perfil de arranque"],
        ["Last selected profile", "Zuletzt ausgewähltes Profil", "Último perfil seleccionado", "Dernier profil sélectionné", "Último perfil selecionado"]
    ];

    private readonly int column;

    public UiLanguage(string choice)
    {
        if (choice == "auto")
        {
            choice = GlobalizationPreferences.Languages
                .Select(tag => tag.Split('-', 2)[0].ToLowerInvariant())
                .FirstOrDefault(code => Array.IndexOf(Codes, code) > 0) ?? "en";
        }
        column = Math.Max(0, Array.IndexOf(Codes, choice) - 1);
    }

    public string this[Phrase phrase] => Catalog[(int)phrase][column];
}
