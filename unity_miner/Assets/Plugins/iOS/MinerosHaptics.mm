// Vibraciones finas del iPhone (Taptic Engine) para Isla Minera / Miner Island.
// kind: 0 seleccion, 1 suave, 2 media, 3 fuerte, 4 exito, 5 aviso, 6 error, 7 blanda (con intensidad)
#import <UIKit/UIKit.h>

static UISelectionFeedbackGenerator *selGen;
static UIImpactFeedbackGenerator *impLight, *impMedium, *impHeavy, *impSoft;
static UINotificationFeedbackGenerator *notGen;

static void MinerosHapticsInit(void)
{
    if (selGen != nil) return;
    selGen = [[UISelectionFeedbackGenerator alloc] init];
    impLight = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
    impMedium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
    impHeavy = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
    if (@available(iOS 13.0, *)) impSoft = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleSoft];
    else impSoft = impLight;
    notGen = [[UINotificationFeedbackGenerator alloc] init];
}

extern "C" void MinerosHaptic(int kind, float intensity)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        MinerosHapticsInit();
        float k = intensity < 0.05f ? 0.05f : (intensity > 1.0f ? 1.0f : intensity);
        switch (kind)
        {
            case 0: [selGen selectionChanged]; break;
            case 1: if (@available(iOS 13.0, *)) [impLight impactOccurredWithIntensity:k]; else [impLight impactOccurred]; break;
            case 2: if (@available(iOS 13.0, *)) [impMedium impactOccurredWithIntensity:k]; else [impMedium impactOccurred]; break;
            case 3: if (@available(iOS 13.0, *)) [impHeavy impactOccurredWithIntensity:k]; else [impHeavy impactOccurred]; break;
            case 4: [notGen notificationOccurred:UINotificationFeedbackTypeSuccess]; break;
            case 5: [notGen notificationOccurred:UINotificationFeedbackTypeWarning]; break;
            case 6: [notGen notificationOccurred:UINotificationFeedbackTypeError]; break;
            default: if (@available(iOS 13.0, *)) [impSoft impactOccurredWithIntensity:k]; else [impLight impactOccurred]; break;
        }
    });
}

// "Preparar" los generadores (se llama al empezar un arrastre): la vibracion siguiente sale sin demora.
extern "C" void MinerosHapticPrepare(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        MinerosHapticsInit();
        [selGen prepare]; [impLight prepare]; [impMedium prepare];
    });
}
