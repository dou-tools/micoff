# micmute

Mute global del micrófono para Windows. Un atajo de teclado o un botón lateral del mouse mutea la entrada del micrófono en Windows, para todas las apps a la vez, con un beep corto para cada acción y un indicador en pantalla.

## Requisitos y compilación

- Windows 10 u 11 (probado en Windows 11)
- .NET 10 SDK para compilar, o .NET 10 Desktop Runtime para solo ejecutarlo

```
dotnet publish src -c Release -o publish
```

Quedan dos ejecutables en `publish/`:

- `micmute.exe` abre la app: aparece un ícono de micrófono en la bandeja del sistema.
- `micmute-autostart.exe` hace lo mismo y además la deja en los programas de inicio de Windows, igual que activar "Iniciar con Windows" desde el menú. Conviene abrirlo ya desde su carpeta definitiva.

Solo corre una instancia a la vez.

## Configuración y uso

### Atajos

Por defecto el mute se alterna con `Ctrl+Alt+Shift+M`. Se pueden sumar más desde el ícono de la bandeja (clic derecho > Atajos > Agregar atajo...) apretando la combinación que se quiera:

- cualquier tecla, sola o con `Ctrl`, `Alt`, `Shift` o `Win`
- teclas poco usadas como `F13`-`F24` o `Pausa`
- los botones laterales del mouse (`XButton1` y `XButton2`)

Cuando un botón lateral se usa como atajo, se intercepta y no llega a las demás apps (por ejemplo, deja de hacer "atrás" en el navegador). Si otra app ya tiene registrado un atajo, aparece un aviso y el menú lo marca como ocupado. Para quitar un atajo, se hace clic sobre él en el mismo submenú.

### Bandeja e indicador

- Clic izquierdo en el ícono: alterna el mute.
- Clic derecho: menú con Atajos, Probar beeps, Indicador en pantalla, Iniciar con Windows, Abrir carpeta de configuración y Recargar configuración.

Mientras el micrófono está muteado, abajo a la derecha se ve un indicador "MIC OFF" en rojo; al activarlo aparece "MIC ON" en verde por un instante. El ícono de la bandeja también cambia de color.

### config.json

La configuración vive en `%APPDATA%\micmute\config.json`:

```json
{
  "Hotkeys": ["Ctrl+Alt+Shift+M", "XButton2"],
  "DeviceName": "",
  "BeepVolume": 0.35,
  "Overlay": true
}
```

| Campo | Descripción |
|---|---|
| `Hotkeys` | Atajos, con modificadores `Ctrl`, `Alt`, `Shift`, `Win` y una tecla con su nombre de [`Keys`](https://learn.microsoft.com/dotnet/api/system.windows.forms.keys) de .NET (`M`, `F13`, `Pause`, `XButton2`...) |
| `DeviceName` | Texto que tiene que aparecer en el nombre del micrófono en Windows. Vacío, o si no se encuentra, se usa el micrófono de comunicaciones por defecto |
| `BeepVolume` | Volumen de los beeps, de `0` a `1` |
| `Overlay` | Muestra u oculta el indicador en pantalla |

Para usar sonidos propios, alcanza con dejar `mute.wav` y `unmute.wav` en la misma carpeta. Los beeps por defecto son dos tonos de 70 ms: descendentes al mutear (880 y 440 Hz) y ascendentes al activar (440 y 880 Hz).

Después de editar el archivo a mano, hay que usar "Recargar configuración" desde el menú.

## Limitaciones

Esta es la forma soft de encarar el mute: no interactúa con el micrófono para apagarlo, sino que mutea su entrada en Windows.

- **El mute del dispositivo es independiente.** Si el micrófono tiene su propio botón de mute, ese estado no se sincroniza con el de micmute.
- **Un micrófono a la vez.** Se mutea el que indica `DeviceName` o, si no, el de comunicaciones por defecto.
- **Atajos ocupados.** Un atajo que ya registró otra app no se puede usar.
- El indicador puede no verse sobre juegos en pantalla completa exclusiva.

## Estructura

```
src/
├── Program.cs                   # Punto de entrada, una sola instancia y variante autostart
├── micmute.csproj               # Proyecto WinForms .NET 10 y opciones de publicación
├── TrayApp.cs                   # Ícono de bandeja, menú y flujo de mute
├── AudioEndpoint.cs             # Busca el micrófono y lee o cambia su mute (Core Audio vía COM)
├── HotkeyManager.cs             # Atajos globales de teclado y hook de botones laterales
├── Hotkey.cs                    # Atajos como texto ("Ctrl+Alt+Shift+M", "XButton2")
├── HotkeyDialog.cs              # Ventana para capturar un atajo nuevo
├── Beeps.cs                     # Genera y reproduce los beeps, o los .wav propios
├── Overlay.cs                   # Indicador MIC ON / MIC OFF, sin foco y sin capturar clics
├── TrayIcons.cs                 # Ícono del micrófono según estado y tema de la barra de tareas
└── Config.cs                    # config.json e inicio con Windows
```
