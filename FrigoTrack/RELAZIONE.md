RELAZIONE TECNICA DI PROGETTO
Modulo: Tecnologie e Progettazione di Sistemi Informatici e di Telecomunicazioni (TEP)
Titolo: Sistema di Telemetria Sicura per FrigoTrack S.r.l.
Studente: [Il Tuo Nome]


1. ARCHITETTURA E SCELTE TECNICHE

Il progetto risolve il problema della trasmissione di dati sensibili (temperatura e umidita di farmaci e alimenti) su un canale HTTP non sicuro, implementando la protezione della comunicazione direttamente a livello applicativo.

La soluzione e stata sviluppata in ambiente C# utilizzando le classi native HttpListener per il server ed HttpClient per la simulazione del client.

La sicurezza e strutturata in tre fasi:
- Scambio di chiavi: E stato implementato l'algoritmo Diffie-Hellman mediante l'uso di BigInteger per calcolare la chiave segreta condivisa K = B^a mod p senza mai far viaggiare la chiave segreta in rete.
- Cifratura simmetrica ed integrita: I dati vengono cifrati con algoritmo AES a 256 bit in modalita CBC. Per evitare manomissioni dei dati durante il transito, viene calcolato un tag di autenticazione HMAC-SHA256 applicato alla busta cifrata.
- Validazione dei dati: Ogni JSON decifrato viene validato con la libreria JSON Schema prima di essere registrato, scartando qualsiasi messaggio con campi errati o non conformi allo standard definito.


2. GESTIONE DEGLI ALLARMI

Durante la ricezione dei dati di telemetria, il server verifica la temperatura inviata rispetto al tipo di merce trasportata:
- Categoria "farmaci": Range di sicurezza compreso tra 2.0 °C e 8.0 °C.
- Categoria "surgelati": Soglia massima di temperatura consentita pari a -18.0 °C.

Nel caso in cui la temperatura non rientri in questi parametri, il server imposta il flag allarme su true ed inserisce una nota esplicativa nel messaggio di risposta cifrato.


3. LIMITI NOTI DELLA SOLUZIONE

- Mancanza di autenticazione del server: L'handshake Diffie-Hellman base non protegge da attacchi di tipo Man-In-The-Middle (MITM) nel momento iniziale di connessione; in un sistema di produzione servirebbe una firma digitale RSA o un certificato per validare l'identita del server.
- Salvataggio dati su file: Le misure ricevute vengono salvate su semplici file di testo JSON invece di utilizzare un database relazionale strutturato.
- Gestione della memoria delle sessioni: Le sessioni rimangono salvate nel dizionario del server finche non viene richiamato l'endpoint di chiusura; in produzione andrebbe previsto un meccanismo di timeout automatico per le sessioni inattive.