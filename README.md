# Universal-Canvas-UI-Manager-HDRP-
Universal Canvas & UI Manager (HDRP)
Bu sistem, oyun içindeki UI geçişlerini (Envanter, Notlar, Silah Tekerleği vb.) merkezi olarak yöneten modüler bir Singleton yapısıdır.

Özellikler:

HDRP Entegrasyonu: UI açıldığında arka planı otomatik olarak bulanıklaştırır (Depth of Field).

Akıllı Kapatma (Canvas Group Modu): Silah tekerleği gibi arkada kodunun sürekli çalışması gereken menüleri objeyi kapatmak yerine CanvasGroup üzerinden şeffaflaştırarak gizler.

Modüler Oyuncu Kontrolü: Kendi hareket sisteminizi (First Person, Third Person vs.) koda müdahale etmeden Inspector üzerinden OnPlayerControlToggled event'ine bağlayarak UI açıldığında oyuncuyu durdurabilirsiniz.

Kurulum:

Scripti sahnede boş bir Manager objesine atın.

Canvas Group Only Canvases listesine, kapanmaması gereken menülerin adını yazın.

Oyuncu kontrol scriptlerinizin (Movement, MouseLook) enabled özelliklerini UnityEvent üzerinden bağlayın.
