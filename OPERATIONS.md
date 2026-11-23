# Üzemeltetési eljárások

Ez a dokumentum azokat az eljárásokat rögzíti, amelyeket az adatkezelési tájékoztató
**megígér** a felhasználóknak. Nem fejlesztői dokumentáció: akkor kell elővenni, amikor
visszaállítás, adatvédelmi incidens, törlési kérés vagy adatkérés történik.

A tájékoztató és a feltételek forrása: `frontend/src/routes/PrivacyPolicy.svelte` és
`Terms.svelte`. Ha itt bármi változik, azokat is **frissíteni kell**.
---

## 1. Biztonsági mentés

### Politika

| Kérdés | Érték | Hol ígértük meg |
|---|---|---|
| Mit mentünk | A teljes PostgreSQL adatbázis **és** az objektumtároló volume-ja, `seaweed_data` (felhasználói csatolmányok) | — |
| Hova | Backblaze B2, `PMA-Backups` bucket, EU-régió | Tájékoztató, 4. pont (adatfeldolgozók) |
| Hogyan | Dokploy beépített backup funkciója | — |
| Megőrzési idő | **Legfeljebb 7 nap**, utána felülíródik | Tájékoztató, 5. pont |
| Titkosítás | **SSE-B2**: nyugalmi állapotban, a Backblaze által kezelt kulcsokkal | Tájékoztató, 4. és 7. pont |
| Felhasználás | **Kizárólag** üzemzavar utáni helyreállítás | Tájékoztató, 5. pont |

### Miért 7 nap

A törlési kérés teljesítési határideje 30 nap. Mivel a mentés ennél jóval hamarabb elévül, egy
törölt felhasználó adata **a határidőn belül magától eltűnik** a mentésekből is — nem kell
sebészileg átírni az archívumokat.

Ez fontos tisztázás: **nem igaz**, hogy a mentés nem tartalmazhat személyes adatot. A GDPR
32. cikk (1) c) pontja kifejezetten elvárja a helyreállíthatóságot, és egy felhasználói adat
nélküli mentés használhatatlan lenne. A valódi kérdés a törlés és a mentés viszonya — arra
válaszol a 2. fejezet.

### Mit jelent az SSE-B2, és mit nem

A `PMA-Backups` bucketen a Backblaze webes felületén bekapcsolt "Encryption: Enabled"
beállítás **SSE-B2**-t jelent: minden feltöltött fájl egyedi kulccsal titkosítódik, a
kulcsokat pedig **a Backblaze kezeli**. Az SSE-C (ahol a kulcs nálunk maradna) bucket-szinten
nem állítható be, csak fájlonként, feltöltéskor - amit a Dokploy backup funkciója nem tesz meg.

**Amire elég:** a GDPR 32. cikk "titkosítás nyugalmi állapotban" elvárása teljesül. Véd a
fizikai adathordozó ellopása és a nem megfelelő selejtezés ellen. Ez a szokásos, elfogadott
szint.

**Amit NEM jelent:** azt nem állíthatjuk, hogy a tárhelyszolgáltató nem fér hozzá a
tartalomhoz. A kulcs náluk van, tehát hozzáfér - és jogi kényszer esetén kényszeríthető is rá.
A tájékoztató szövege ezért **nem** ígér ilyet; ha valaki mégis ilyen igényt támaszt, ahhoz
kliensoldali titkosítás kellene, ami a Dokploy backup funkcióján keresztül nem megoldható.

Mivel a Backblaze anyavállalata amerikai, a maradék kockázatot a szolgáltatóval kötött
**adatfeldolgozói szerződés** (DPA) rendezi. Ennek elfogadása az indulási ellenőrzőlistán van.

### A jelenlegi működés

A mentés nem a repóból fut: nincs mentési szkript a kódbázisban, a Dokploy beépített backup
funkciója végzi. A PostgreSQL automatikusan felismerhető, az objektumtároló pedig volume-szinten
mentődik - **ugyanabba a bucketbe**.

Figyelem: ez a volume a felhasználók által feltöltött fájlokat tartalmazza, vagyis a
mentés legkiszámíthatatlanabb tartalmú része. Bármi lehet benne, amit egy felhasználó egy
taskhoz csatolt.

> ### Tárolócsere után a mentési célt ÁT KELL ÁLLÍTANI
>
> A mentés **egy volume nevére** van konfigurálva a Dokploy felületén, nem a repóban. A
> MinIO → SeaweedFS cserével a volume neve `minio_data`-ról **`seaweed_data`**-ra változott.
>
> Ha a backup job nem kerül át, a napi mentés **továbbra is lefut és zöld marad**, de a
> felhasználói csatolmányok **kimaradnak belőle** - és ez csak egy visszaállításnál derülne ki.
> Ugyanaz a hibaosztály, mint a naplók megőrzési ideje: a konfiguráció máshol él, mint a kód.
>
> Az ellenőrzés egyszerű: a csere utáni első mentés után nézd meg, hogy a csatolmányok
> **benne vannak-e**.

- Ütemezés: **naponta**, a Dokploy backup ütemezője szerint
- A 7 napos rotáció a Dokploy backup beállításánál van megadva
- Utolsó sikeres próba-visszaállítás dátuma: `[KITÖLTENDŐ]`

### A régi mentések nem évülnek el a séma változásától

Kézenfekvő félelem, hogy egy régebbi mentés a migrációk miatt "használhatatlan". **Nem az:**
az API induláskor `MigrateAsync()`-et futtat, ami a visszaállított adatbázist felviszi az
aktuális sémaverzióra. A mentés a `__EFMigrationsHistory` táblát is tartalmazza, tehát a
rendszer pontosan tudja, honnan kell folytatnia.

A fordított irány a veszélyes: egy olyan mentés, amely **újabb**, mint a futó kód. Ilyen a
normál üzemben nem fordul elő, csak ha valaki visszaállít egy korábbi alkalmazásverziót.

> Egy mentés, amit soha nem állítottak vissza, nem mentés, hanem remény. Érdemes évente
> legalább egyszer kipróbálni, és a dátumot ide beírni.

---

## 2. Visszaállítás — és a törlések újraalkalmazása

**Ez a fejezet a legfontosabb az egész dokumentumban.**

A fióktörlés anonimizálás: a `User` record megmarad, de a név, az e-mail cím, a jelszó és a
kétfaktoros kulcs megsemmisül. Ha egy **korábbi** mentésből állítunk vissza, a törölt
felhasználó adata **visszatér** — a mentés még a törlés előtti állapotot őrzi.

Ezért minden visszaállítás után újra kell alkalmazni azokat a törléseket, amelyek a mentés
időbélyege óta történtek.

### Honnan tudjuk, kit töröltek

A fióktörlés strukturált naplóbejegyzést ír az OpenObserve-be:

```
UserErasure | UserId: ... | ErasedAt: ... | Törölt refresh tokenek: ... |
Törölt jelszó-tokenek: ... | Átírt activity sorok: ...
```

Az `EventType` **külön strukturált mező**, nem csak a szöveg része — ezért mezőegyezésre
lehet keresni, nem részegyezésre. A lekérdezés az OpenObserve felületén, SQL módban:

```sql
SELECT _timestamp, userid, erasedat, refreshcount, resetcount, activitycount
FROM default
WHERE eventtype = 'UserErasure'
ORDER BY _timestamp DESC
```

**A mezőnevek csupa kisbetűsek, aláhúzás nélkül.** A Serilog tulajdonságneveiből az OTLP-úton
`EventType` → `eventtype`, `UserId` → `userid` lesz — nem `event_type` és nem `attributes_`
előtaggal. Ez a v1.0.0 image-en **méréssel ellenőrizve**, nem feltételezés.

További mezők, amelyek minden sornál megvannak:

| Mező | Tartalma |
|---|---|
| `body` | a renderelt üzenet (itt ezzel kezdődik: `UserErasure \| UserId: ...`) |
| `message_template_text` | a Serilog message template, behelyettesítés előtt |
| `severity` | `Information`, `Warning`, ... |
| `service_name` | `projectmanager-api` |
| `service_instance_id` | **melyik replika** írta a sort (a konténer azonosítója) |
| `_timestamp` | mikroszekundumban, nem milliszekundumban |

Tartalék út, ha a strukturált mező valamiért hiányzik (pl. régi, a csere előtt írt sorok):
`WHERE body LIKE '%UserErasure%'`.

### Ellenőrzőlista visszaállítás után

1. Az adatbázis visszaállt, az API elindult (a migrációk automatikusan lefutnak).
2. OpenObserve-ben lekérdezni az összes `UserErasure` bejegyzést a **mentés időbélyege óta**.
3. Minden érintett `UserId`-ra újra elvégezni az anonimizálást — a `DeleteAccountAsync`
   logikája szerint: `Email` -> `deleted-{Id}@invalid.local`, `DisplayName` ->
   `<Törölt felhasználó>`, `PasswordHash` -> új véletlen, `TotpSecret` /
   `EmailVerificationToken` -> `null`, `IsTotpEnabled` / `IsEmailVerified` / `IsActive` ->
   `false`, `DeletedAt` -> az eredeti törlés időpontja.
4. A hozzájuk tartozó `RefreshToken` és `PasswordResetToken` sorokat törölni.
5. Az érintett projektek activity-leírásaiban a régi nevet lecserélni.
6. A visszaállítás tényét és az újraalkalmazott törléseket **feljegyezni** — ez bizonyítja,
   hogy a törlési kérést teljesítettük.

A helyettesítő értékek egy helyen élnek a kódban:
`backend/src/ProjectManager.API/Common/Constants/UserAnonymization.cs`.

### A napló megőrzési ideje — kritikus csatolás

**A naplók megőrzési ideje pontosan 30 nap, és ez nem szabadon választható.** Két oldalról
van beszorítva, alulról és felülről is:

| Korlát | Érték | Honnan |
|---|---|---|
| Mentés megőrzése | 7 nap | a mentési ciklus |
| **Alsó korlát** a naplóra | legalább 30 nap | ha a napló hamarabb évül el, mint a legrégebbi visszaállítható mentés, elveszítjük annak nyilvántartását, kit kell újra anonimizálni — és egy visszaállítás némán feltámasztana egy törölt fiókot |
| **Felső korlát** a naplóra | 30 nap | az adatkezelési tájékoztató ennyit ígér a „Technikai naplók (IP címmel)" sorban. Ez **jogi vállalás**, nem üzemeltetési preferencia |

A két korlát egyetlen értékben találkozik, tehát **30 nap**, se több, se kevesebb.

A retenció **env var**, a `docker-compose.prod.yml`-ben:

```yaml
ZO_COMPACT_DATA_RETENTION_DAYS: 30
```

**Ez a csere legfontosabb részlete.** Az OpenObserve alapértelmezése **3650 nap** (~10 év),
tehát a változó kihagyása nem „kicsit hosszabb megőrzést" jelentene, hanem azt, hogy a rendszer
**csendben megsérti a saját adatkezelési tájékoztatóját** — tízéves naplómegőrzéssel, IP
címekkel. A korábbi Seq alapértelmezése véletlenül pont 30 nap volt, ezért ott egy kihagyott
beállítás nem járt következménnyel; itt jár.

Cserébe a beállítás **verziókezelt**: a compose fájl átnézésével ellenőrizhető, nem egy webes
felület mélyén él. Korábban ez kézi lépés volt, és az indulási ellenőrzőlistán nyitott tételként
szerepelt.

### A naplót NE vedd bele a mentésbe

Ez elsőre ellentmondásosnak tűnik, de fontos: a mentés ma csak a PostgreSQL-t és a tároló
volume-ot viszi, a naplót nem — és **ez így helyes**.

Ha a napló volume is a mentés része lenne, egy visszaállítás a naplót is visszagörgetné
ugyanarra az időpontra, mint az adatbázist. Pontosan azok a `UserErasure` bejegyzések vesznének
el, amelyekre a fenti ellenőrzőlista épül. Az aggregátor értéke itt éppen az, hogy **túléli** az
adatbázis visszaállítását.

### A naplófelület elérése — eldöntendő

A csere oka az volt, hogy **egynél több ember is nézhesse a naplókat**. A hálózati beállítás
viszont ezt egyelőre nem teszi lehetővé: a konténer — ahogy korábban a Seq — csak
`127.0.0.1:5080`-on hallgat, tehát a felület kizárólag a szerverről vagy SSH-tunnelen érhető el.

Ez **szándékos alapállapot**, nem feledékenység: egy naplófelület érzékeny adatot tartalmazhat,
és a publikus kitétele önálló biztonsági döntés. Két út van:

| Megoldás | Mérleg |
|---|---|
| **Marad loopback** | Semmi új támadási felület. Viszont minden olvasónak SSH-hozzáférés kell a VPS-hez, ami a többfelhasználós képesség nagy részét elveszi |
| **Traefik-útvonal védett domainen** | Valódi többfelhasználós elérés. Ehhez viszont a többi szolgáltatáshoz hasonló címkék kellenek, és **az OpenObserve saját belépése lesz az egyetlen védelem** — tehát erős jelszavak kötelezőek |

A nyílt kiadásban **nincs finomhangolt jogosultságkezelés**: aki belép, mindent lát. Ezt a
döntés előtt érdemes tudni.

### Maradékkockázat

| Eset | Mi történik |
|---|---|
| Csak az adatbázis sérül vagy romlik el | A napló él, a törlési nyilvántartás megvan, az eljárás működik. **Ez a gyakori eset.** |
| Teljes gépvesztés | A napló is odavész, a mentés óta történt törlésekről nincs nyilvántartás |

A második eset **legfeljebb 7 napnyi** törlést érinthet (a mentési ciklus hossza), és két
valószínűtlen esemény egybeesését igényli: teljes gépvesztést **és** egy fióktörlést abban az
ablakban. Ezt a kockázatot ekkora szolgáltatásnál tudatosan vállaljuk.

A visszaállítás automatizálására a Dokploy nem ad lehetőséget, és nem is várható el tőle: ez
alkalmazás-specifikus üzleti logika, nem mentési funkció. A GDPR nem ír elő automatizálást —
egy dokumentált, ismert és követhető kézi eljárás legitim kontroll, különösen egy ilyen
ritka, katasztrófa-jellegű eseménynél.

Olcsó szokás, ami tovább csökkenti a kockázatot: ha valaha ténylegesen érkezik törlési kérés,
érdemes feljegyezni a rendszeren **kívül** is — egyetlen sor egy jegyzetben elég.

---

## 3. A példányszám megváltoztatása

A futó API példányok számát **egyetlen** változó deklarálja, az `API_REPLICAS`. Ugyanez a
változó adja a `docker-compose.prod.yml` `deploy.replicas` értékét **és** az alkalmazás saját
tudását is róla, hogy a kettő ne tudjon elcsúszni. Három dolog függ tőle, ezért a módosítása
nem csak egy szám átírása.

### Mi következik a példányszámból

| Példányszám | Kérelemkorlátozás | SignalR | DB pool / példány |
|---|---|---|---|
| 1 | a folyamat memóriájában | nincs backplane | 80 |
| 2 (jelenlegi) | **Redis kötelező** | Redis backplane | 40 |
| 3 | **Redis kötelező** | Redis backplane | 26 |

A pool képlete: `(POSTGRES_MAX_CONNECTIONS - 20) / API_REPLICAS`. A 20 tartalék a mentés
`pg_dump`-ja, egy üzemeltetői `psql` munkamenet és az induláskor futó migráció számára van.
A fenti táblázat a `POSTGRES_MAX_CONNECTIONS` alapértékére (100) szól.

**A `max_connections` egyetlen helyen állítható.** A `POSTGRES_MAX_CONNECTIONS` egyszerre adja
a postgres konténer indítási paraméterét és a fenti származtatást, tehát a kettő nem tud
elcsúszni egymástól. Beállítás nélkül mindkettő 100, ami a PostgreSQL alapértelmezése.

Ez akkor válik érdekessé, ha az adatbázis **saját gépre** kerül. Ma a 100 nem a rendszer
tulajdonsága, hanem azé a gépé, amelyen az adatbázis az alkalmazással együtt fut — és amíg ez a
szám adott, az `API_REPLICAS` emelése nem ad kapacitást, csak **felosztja ugyanazt a 100
kapcsolatot** (lásd a táblázat harmadik sorát: három példánynál már csak 26 jut egyre). Külön
adatbázis-kiszolgálón a `POSTGRES_MAX_CONNECTIONS` emelése az, ami a példányszám növelését
tényleges kapacitássá fordítja.

### Mire kell figyelni

- **Egyről többre emelve a `REDIS_CONNECTION` kötelezővé válik.** Ha nincs beállítva, az API
  **indulásnál dob**, érthető üzenettel. Ez szándékos: megosztott számláló nélkül minden
  példány külön számolna, tehát a tényleges korlát a példányszámmal felszorozódna — csendben.
- **Egyre csökkentve a korlátok a memóriában számolnak.** Ez egy példánnyal helyes, de az
  újraindítás nullázza a számlálókat. Az indulási naplóban ilyenkor egy `WRN` sor áll
  (`RateLimitInMemory`), ami kimondja a módot.
- **A `docker compose up --scale pm-api=N` NEM elég.** Felülírja a `deploy.replicas`-t, de az
  env vart nem — az alkalmazás ilyenkor egypéldányosnak hiszi magát, és a korlátok
  példányonként számolnak. Mindig az `API_REPLICAS`-t állítsd át.
- **Nyolc példány felett** a 10-es alsó pool-korlát miatt a példányok együtt több kapcsolatot
  kérhetnének, mint a `max_connections`. Ilyenkor az indulási napló egy `WRN` sorban
  (`DbPoolFloorReached`) megadja a szükséges értéket, amit a `POSTGRES_MAX_CONNECTIONS`-ba kell
  beírni. Ugyanez a `WRN` sor szól akkor is, ha a megadott érték eleve kisebb a 20-as
  tartaléknál.
- **A `POSTGRES_MAX_CONNECTIONS` emelése után a postgres konténert újra kell indítani**, nem
  csak az API-t: a `max_connections` indítási paraméter, menet közben nem vehető fel. Ha csak az
  API indul újra, a pool a nagyobb értékből származtat, a kiszolgáló viszont még a régi korláttal
  fut — és ez pont az a `53300: too many clients already` hiba, amit a méretezés megszüntet.

### A napló ellenőrzése deploy után

Az OpenObserve-ben a mód egyetlen lekérdezéssel ellenőrizhető:

```sql
SELECT * FROM default WHERE eventtype = 'RateLimitInMemory'
```

Két replika mellett ez **nem adhat találatot**. Ha mégis ad, akkor vagy az `API_REPLICAS`
maradt 1-en, vagy a példányszám kézi skálázással lett megemelve — mindkét esetben a
kérelemkorlátozás a vártnál engedékenyebb.

## 4. Adatvédelmi incidens

Adatvédelmi incidens minden olyan biztonsági esemény, amely személyes adat véletlen vagy
jogellenes megsemmisítését, elvesztését, megváltoztatását, jogosulatlan közlését vagy az
azokhoz való jogosulatlan hozzáférést eredményezi.

### Határidő

**72 óra** a tudomásszerzéstől a NAIH felé, ha az incidens valószínűsíthetően kockázattal jár.
Ha magas kockázattal jár, az érintetteket is értesíteni kell, indokolatlan késedelem nélkül.

### Teendők sorrendben

1. **Megállítani.** A folyamatban lévő hozzáférést lezárni: érintett kulcsok cseréje
   (`JWT_SECRET`, `ENCRYPTION_KEY`, adatbázis-jelszó), szükség esetén a szolgáltatás
   ideiglenes leállítása.
2. **Rögzíteni.** Mikor derült ki, mi történt, mely adatkörök érintettek, hány személy,
   milyen következménnyel. Az OpenObserve naplói ehhez a fő forrás — ne töröld őket.
3. **Értékelni.** Jár-e kockázattal az érintettekre? Ha a kiszivárgott adat titkosított volt
   és a kulcs nem érintett, a kockázat jelentősen alacsonyabb.
4. **Bejelenteni**, ha kockázatos: NAIH, 72 órán belül. A bejelentés akkor is megtehető, ha
   még nem minden részlet ismert — a hiányzó információ utólag pótolható.
5. **Értesíteni** az érintetteket, ha magas a kockázat.
6. **Utólagos elemzés:** mi tette lehetővé, és mi akadályozza meg a megismétlődést.

Kapcsolat: NAIH, `ugyfelszolgalat@naih.hu`, [naih.hu](https://naih.hu)

Az incidenseket akkor is fel kell jegyezni, ha nem bejelentéskötelesek — a nyilvántartás maga
is jogszabályi kötelezettség.

---

## 5. Érintetti kérések teljesítése

A tájékoztató hat érintetti jogot sorol fel. Kettő **önkiszolgáló**: a törlés a fiók
törlésével, a helyesbítés nagyrészt a profil szerkesztésével elvégezhető. A másik négy —
hozzáférés, adathordozhatóság, a kezelés korlátozása, tiltakozás — **kérelemre** teljesül, a
tájékoztatóban megadott e-mail címen, **legfeljebb 30 napon belül**, díjmentesen.

Ez a szakasz a két adatkiadással járó kérést írja le: a **hozzáférést** (GDPR 15. cikk) és az
**adathordozhatóságot** (20. cikk). Automatizált adatkiadó végpont szándékosan nincs — lásd a
szakasz végét.

### 5.1. Először azonosítás, utána adat

Adatot **csak a fiókon szereplő e-mail címre** adunk ki, és csak arról érkezett kérésre. Ha a
kérés más címről jön, vissza kell írni, hogy küldje a regisztrált címről — vagy jelentkezzen be
és onnan igazolja. Kétség esetén a rendelet kifejezetten engedi további adat kérését az
azonossághoz.

Ez nem formalitás: egy hozzáférési kérés válasza **egy helyen tartalmaz mindent** egy
személyről. Rossz címre kiküldve maga a teljesítés lenne az adatvédelmi incidens.

### 5.2. Mit tartalmaz a válasz, és mit nem

| Adatkör | Hozzáférés (15.) | Hordozhatóság (20.) |
|---|---|---|
| Fiókadatok | igen | igen |
| Projekttagságok, szerepkörök | igen | igen |
| Saját hozzászólások | igen | igen |
| Létrehozott feladatok, hozzárendelések | igen | igen |
| Feltöltött fájlok adatai | igen | igen |
| Feltételek elfogadása | igen | igen |
| **Tevékenységnapló** | igen | igen — megfigyelt adat |
| Munkamenet- és műveleti nyomok | igen, **érték nélkül** | nem — hitelesítő adat |

A hordozhatóság hatóköre az, amit az érintett „rendelkezésre bocsátott", és ezt a hivatalos
iránymutatás (29. cikk szerinti munkacsoport, WP242) **szélesen** értelmezi: nem csak a
tudatosan megadott adat tartozik bele, hanem a szolgáltatás használatából **megfigyelt** adat
is — a tevékenységnapló nevesítve ilyen. Kimarad belőle a **származtatott** adat; ebben a
rendszerben a kimutatások tartoznának ide, azok viszont nem is tárolódnak, hanem lekérdezéskor
állnak elő.

A gyakorlatban ezért a két kérésre **ugyanaz a kimenet** készül, géppel olvasható alakban: a
két jog hatóköre itt csak a hitelesítő adatoknál válik el, azok viszont egyik kérésben sem
adhatók ki.

**Amit soha nem adunk ki:**

- **a jelszó lenyomatát és a kétfaktoros titkot** — ezek hitelesítő adatok; kiadásuk a fiók
  biztonságát rontaná, és a másolat joga nem terjed ki odáig;
- **a tokenek értékét** (munkamenet, jelszó-visszaállító, e-mail megerősítő) — ezekből a
  *létezés* és az időbélyeg kerül a válaszba, az érték nem;
- **más személyek adatait.** Ez a legkönnyebben elvétett pont: a tevékenységnapló sorai
  gyakran két emberre vonatkoznak („X eltávolította Y-t"). A kérelmezőnek szóló másolatban a
  **másik fél neve nem szerepelhet** — helyette a szerepe (például „egy projekttag").

### 5.3. A lekérdezések

Az érintett azonosítója az e-mail címből:

```sql
SELECT "Id", "Email", "DisplayName", "IsActive", "DeletedAt"
FROM "Users" WHERE lower("Email") = lower('kerelmezo@example.com');
```

A többi lekérdezés ezt az azonosítót használja. A tábla- és oszlopnevek nagybetűsek, tehát a
`psql`-ben **idézőjel kell** körülöttük.

```sql
\set uid '''00000000-0000-0000-0000-000000000000'''

-- Fiókadatok (a jelszólenyomat és a 2FA-titok szándékosan kimarad)
SELECT "Id", "Email", "DisplayName", "IsEmailVerified", "IsTotpEnabled",
       "CreatedAt", "UpdatedAt", "DeletedAt"
FROM "Users" WHERE "Id" = :uid;

-- Projekttagságok és szerepkörök
SELECT p."Name", p."ProjKey", pm."ProjectRole", pm."JoinedAt"
FROM "ProjectMembers" pm
JOIN "Projects" p ON p."Id" = pm."ProjectId"
WHERE pm."UserId" = :uid;

-- Tulajdonolt projektek
SELECT "Id", "Name", "ProjKey", "CreatedAt" FROM "Projects" WHERE "OwnerId" = :uid;

-- Saját hozzászólások (a 20. cikk szerinti tartalom)
SELECT c."Id", t."TaskKey", c."Body", c."CreatedAt", c."UpdatedAt"
FROM "Comments" c
JOIN "ProjectTasks" t ON t."Id" = c."TaskId"
WHERE c."UserId" = :uid ORDER BY c."CreatedAt";

-- Létrehozott feladatok
SELECT "Id", "TaskKey", "Title", "Description", "CreatedAt"
FROM "ProjectTasks" WHERE "CreatedById" = :uid ORDER BY "CreatedAt";

-- Hozzárendelések
SELECT t."TaskKey", t."Title", ta."AssignedAt"
FROM "TaskAssignments" ta
JOIN "ProjectTasks" t ON t."Id" = ta."TaskId"
WHERE ta."UserId" = :uid;

-- Feltöltött fájlok adatai (a fájlok tartalma kérésre, külön)
SELECT "Id", "FileName", "ContentType", "SizeBytes", "Version", "CreatedAt"
FROM "Attachments" WHERE "UploadedById" = :uid ORDER BY "CreatedAt";

-- A feltételek elfogadása
SELECT tv."Version", tv."EffectiveFrom", uta."AcceptedAt"
FROM "UserTermsAcceptances" uta
JOIN "TermsVersions" tv ON tv."Id" = uta."TermsVersionId"
WHERE uta."UserId" = :uid ORDER BY uta."AcceptedAt";

-- Tevékenységnapló: CSAK a hozzáférési kéréshez
SELECT "Id", "ProjectId", "EntityType", "Action", "Description", "CreatedAt"
FROM "Activities"
WHERE "ActorId" = :uid OR "TargetUserId" = :uid
ORDER BY "CreatedAt";

-- Munkamenet- és műveleti nyomok: a tokenek ÉRTÉKE nélkül
SELECT "Id", "CreatedAt", "ExpiresAt", "IsRevoked", "RevokedAt"
FROM "RefreshTokens" WHERE "UserId" = :uid;

SELECT "Id", "ExpiresAt", "IsUsed" FROM "PasswordResetTokens" WHERE "UserId" = :uid;

SELECT "Id", "FileName", "CreatedAt", "Confirmed", "ExpiresAt"
FROM "PresignedUrlLogs" WHERE "CreatedById" = :uid;
```

**Egy figyelmeztetés a tevékenységnaplóhoz.** A `Description` nem kész szöveg, hanem sablon:
`{actor}` és `{target}` jelölőket tartalmaz, amelyeket az alkalmazás olvasáskor cserél ki az
**aktuális** nevekre. A kiadás előtt ezeket fel kell oldani — a kérelmezőre a saját nevét, a
másik félre pedig szerepmegnevezést írva (5.2.). A jelölők bevezetése előtt keletkezett sorok
kész szöveget tartalmaznak, azokban a nevet kézzel kell kivenni.

### 5.4. A kimenet, a határidő és a nyilvántartás

A válasz **egyetlen JSON fájl** vagy CSV-ket tartalmazó ZIP — a tájékoztató a
hordozhatóságnál géppel olvasható formátumot ígér, és ez mindkét kérésre megfelel. A feltöltött
fájlok tartalma nem kerül bele automatikusan; ha a kérelmező azt is kéri, a tárolóból külön
kell kigyűjteni a fenti lista `StorageKey` értékei alapján.

**A határidő a kérés beérkezésétől számított egy hónap.** A teljesítést fel kell jegyezni — a
kérés dátuma, a kérelmező, és hogy mit adtunk ki. Két okból: a megfelelés igazolásához, és
mert a visszaállítási eljárás (2. szakasz) is ebből a nyilvántartásból tudja, mit kell egy
régebbi mentés visszatöltése után újra elvégezni.

### 5.5. Miért nincs erre végpont

A rendelet a hozzáférést és a hordozhatóságot **kérésre** teljesítendő kötelezettségként
határozza meg, nem önkiszolgáló felületként. A tájékoztató ennek megfelelően kérelemalapú
folyamatot ígér határidővel, és ez a folyamat teljesíthető — ez a szakasz maga a bizonyíték rá.

Az automatizálás ellen két érv is szól. Egy adatkiadó végpont lenne a rendszer **legvonzóbb
célpontja**: egyetlen híváson adna vissza mindent egy személyről, tehát ott a
jogosultság-ellenőrzésnek hibátlannak kellene lennie, és a fenti kizárásokat (hitelesítő
adatok, harmadik felek nevei) kódban kellene kikényszeríteni. A felhasználói kör emellett
ismert és kisszámú, így a kézi teljesítés terhe elhanyagolható.

Ha ez változik, a kezdő lépés az **összegyűjtés** automatizálása: a fenti lekérdezések egy
olvasási végpont mögé rendezve. A nehéz rész viszont nem az összegyűjtés, hanem a
**visszatartás** — a hitelesítő adatok, a tokenek értéke és a harmadik felek nevei —, és ezt a
döntéssorozatot kódban kell kikényszeríteni, nem a teljesítő ember figyelmére hagyni. Ez az
oka, hogy a végpont nem a leltár helyett, hanem csak utána következhet.

---

## 6. Éles indulás előtti ellenőrzőlista

A jogi dokumentumok olyan állításokat tartalmaznak, amelyeknek az indulás pillanatában
**igaznak kell lenniük**. Ez a lista gyűjti össze, mi van még nyitva.

- [ ] **A négy `VITE_LEGAL_*` környezeti változó beállítva az ÉLES környezetben**: adatkezelő
      neve, levelezési címe, kapcsolattartási e-mail címe, tárhelyszolgáltató neve és címe.
      Ha üresen maradnak, a dokumentumokban feltűnő `[KITÖLTENDŐ: ...]` helyőrző látszik.
      A változók leírása a `.env.example`-ben
- [x] ~~**A NAIH elérhetőségei ellenőrizve.**~~ Egyeznek a naih.hu-val: 1055 Budapest,
      Falk Miksa utca 9-11.; postacím 1363 Budapest, Pf.: 9.; ugyfelszolgalat@naih.hu
- [x] ~~**A Backblaze adatfeldolgozói szerződése (DPA).**~~ Nincs teendő: a Backblaze a DPA-t
      kifejezetten **beépíti a szerződési feltételeibe**, amiket a fiók létrehozásakor
      elfogadtunk. Külön aláírni nem kell; az EGT-re vonatkozó szöveg a
      `backblaze.com/company/policy/dpa-for-eea-eu-residents` címen olvasható
- [ ] **A napló megőrzési ideje ellenőrizve az ÉLES példányon.** Már nem kézi beállítás: a
      `ZO_COMPACT_DATA_RETENTION_DAYS: 30` a `docker-compose.prod.yml`-ben van, tehát
      verziókezelt. A deploy után **egyszer** ellenőrizd, hogy tényleg érvényre jutott — az
      alapértelmezés 3650 nap lenne, ami megsértené az adatkezelési tájékoztatót
- [x] ~~**A mentés célja átállítva** a `seaweed_data` volume-ra.~~ Elvégezve, és a következő
      mentés **ellenőrizve a Backblaze-en** - a csatolmányok benne vannak. Ez volt a
      tárolócsere egyetlen olyan hibája, ami csendben adatvesztéshez vezetett volna
- [x] ~~**Az objektumtároló S3 kulcsai beállítva**~~ (`S3_ACCESS_KEY` / `S3_SECRET_KEY`),
      és a `${VAR:?}` alak miatt a deploy **elbukna** nélkülük. Hiányukban a SeaweedFS S3
      API-ja hitelesítés nélkül futna - és a tároló a presigned URL-ek miatt publikusan elérhető
- [x] ~~**Az admin felület jelszava beállítva**~~ (`S3_ADMIN_PASSWORD`, felhasználónév: `admin`).
      Üres értéknél a SeaweedFS **hitelesítés nélkül** szolgálná ki az admin felületet, és ez
      csendben történik. A `${VAR:?}` alak miatt a deploy most elbukik, ha kimarad
- [ ] **A git integrációk hozzáférési tokenjei ellenőrizve**, ha van olyan integráció,
      amelynél a csonkolt pushok pótlását használni akarjuk. Privát tárháznál token nélkül a
      pótlás nem működik, a lejárt token pedig csak a KÍSÉRLET pillanatában derül ki -
      és ha az automatikus pótlás be van kapcsolva, azt a kísérletet senki nem nézi.
      A sorok állapota a 8. szakasz lekérdezésével kérdezhető meg
- [ ] **KÜLÖN betöltő felhasználó** az OpenObserve felületén, az `OO_INGEST_USER` /
      `OO_INGEST_TOKEN` változókhoz. **A naplózás már működik** - jelenleg a root hitelesítő
      adataival -, tehát ez nem hiba, hanem higiénia: az admin jelszava ne üljön az API
      konténerében, és a token önállóan rotálható legyen.
      Megjegyzés: a nyílt kiadás **csak `admin` szerepkört** engedélyez (mérve), tehát ez nem
      jogosultság-szűkítés. Lépések: belépés → *Management → Users* → új felhasználó → a két
      változó átírása → **csak a `pm-api`** redeploy (az aggregátort nem kell újraindítani)
- [x] ~~**A naplófelület elérése eldöntve.**~~ Marad **loopback + SSH-tunnel**, Traefik-útvonal
      nélkül - ez a biztonságosabb választás, és a gyakorlatban megoldott. Ugyanígy a
      tárolóé is (`127.0.0.1:23646`). Figyelem: a tunnel **elfedi a helyi portokat**, ezért
      érdemes eltérő helyi portra irányítani (pl. `-L 23647:localhost:23646`), különben a
      fejlesztői példány és az éles összekeveredik
- [x] ~~**A mentés ütemezése rögzítve.**~~ Napi mentés a Dokploy ütemezőjével, 7 napos rotációval
- [x] ~~**Az activity-leírások sablonosítása kész.**~~ Elkészült: a leírások sablont tárolnak,
      a neveket a kiolvasás helyettesíti be, így az anonimizálás magától érvényesül
- [ ] **Próba-visszaállítás elvégezve**, a 2. fejezet ellenőrzőlistájával együtt. Az utolsó a
      kezdeti beállításkor történt; azóta sok migráció jött, ezért érdemes megismételni -
      nem azért, mert a régi mentés érvénytelen lenne (a migráció felviszi az aktuális
      sémára), hanem mert egy soha nem gyakorolt eljárás nem eljárás
- [ ] **2026-11-12 után: a `MessagePack` közvetlen hivatkozása elhagyható.** Ma azért van az
      API `csproj`-jában, mert a `SignalR.StackExchangeRedis 10.0.9` egy sebezhető verziót
      (2.5.301, CVE-2026-92707) hozna be tranzitívan. A `10.0.13` maga is `>= 2.5.303`-at kér,
      de 2026-10-13-án jelent meg, tehát a 30 napos szabály szerint csak **2026-11-12 után**
      vehető fel. Akkor: a SignalR csomag emelése, és a `MessagePack` sor törlése. A triázs
      (miért nem volt sürgős) a `SCHEDULE.md`-ben
- [ ] **Eldöntendő: a CI bukjon-e el NuGet sebezhetőségi figyelmeztetésen.** A `MessagePack`
      NU1902 figyelmeztetése a CI naplójában **ott volt** minden futásnál (a workflow friss
      `dotnet restore`-t futtat, NuGet gyorsítótár nélkül, tehát az audit lefut) - csak
      lokálisan nem látszott, és senki nem nézte meg. Egy kiírt, de következmény nélküli
      figyelmeztetés gyakorlatilag láthatatlan. Megfontolandó a `-warnaserror:NU1901,NU1902,NU1903`
      a build lépésen: akkor egy új sebezhetőségi riasztás megállítja a pipeline-t, nem a
      naplóban ül. Ellenérv: egy harmadik féltől jövő új riasztás így bármikor eltörheti a
      buildet - ezért ez termékdöntés, nem automatikus javítás

> A dokumentumokat jogász nem nézte át. Valódi felhasználókkal induló szolgáltatásnál ez megéri.
> A szakdolgozatban ezt a fenntartást is érdemes jelezni.

---

## 7. Memóriahelyzet, a swap és a hátralévő intézkedések

> **A swap 2026-11-09-én elkészült** - a lenti 2026-10-30-i táblázat `Swap: 0 B` sora ennyiben
> elavult. A beállítást és az első mérést a *„A swap bevezetése"* alszakasz tárgyalja; a
> táblázat teljes újramérése hátravan, lásd *„Ami a mérésből hátravan"*.

Mérés 2026-10-30-án, éles gépen (`docker stats --no-stream`, `free -h`, `vmstat 1 5`):

| Csoport | Memória |
|---|---|
| **Az alkalmazás egésze** (2× API, PostgreSQL, SeaweedFS, OpenObserve, Redis, frontend) | **~829 MiB** |
| **A platform** (Dokploy 966 MiB + saját Postgres 112 + saját Redis 5 + Traefik 34) | **~1117 MiB** |
| Konténerek összesen | ~1946 MiB |
| `free` szerint használt / elérhető | 2.5 GiB / **1.2 GiB** |
| Swap | **0 B — nincs konfigurálva** |

Részletek: a két API példány 204 és 169 MiB, tehát a .NET oldalon **nincs** memóriaprobléma
(a Server GC-re vonatkozó gyanú mérés alapján elvetve, nincs szükség GC-beállításra).
A `vmstat` nem jelez swappolást és io-várakozást, a CPU 92-98%-ban üresjáratban van.

**A platform 35%-kal többet használ, mint amit futtat.** Ez tudatosan vállalt csere: a
Dokploy adja a mentéseket, a Let's Encrypt automatizálást, a git-alapú telepítést és a
beépített Traefiket. A kiváltása ezeket külön-külön pótolná - a mentés a legnehezebb.

### Amiért ez ma nem probléma, és mikor válna azzá

Üzem közben nincs szűkösség: 1.2 GiB elérhető, nincs swappolás. A kockázat a **tartalék
hiánya egy csúcs alatt**, és van egy rendszeresen bekövetkező csúcs: a Dokploy **ugyanezen a
gépen buildel**. A `dotnet publish` és a Vite build több száz MB-ot kér, miközben nincs swap
és nincs konténer-memórialimit. Ilyenkor az OOM killer pontszám szerint választ, tehát a
legnagyobb RSS-ű folyamatot lövi ki - akár a PostgreSQL-t.

A plauzibilis hibaforgatókönyv tehát nem az, hogy üzem közben elfogy a memória, hanem hogy
**egy telepítés kinyír egy futó szolgáltatást**.

### Az intézkedések

**1. Swap hozzáadása** - **ELKÉSZÜLT 2026-11-09-én.** 2 GB fájl swap, `vm.swappiness=10`.
Ez volt a legkisebb beavatkozás és a legnagyobb hatás: a csúcsokat kisimítja ahelyett, hogy
OOM-ot okozzanak. Részletek a következő alszakaszban.

A 2. és a 3. **tudatosan várat magára** - a rendszer ma teszt- és demonstrációs terhelés alatt
fut, ahol a maradékkockázat elfogadható.

**2. Konténer-memórialimitek (`deploy.resources.limits.memory` a compose-ban).** Nem a
fogyasztás csökkentésére, hanem azért, hogy az OOM killer a **hibás** komponenst válassza, ne
a PostgreSQL-t. A fenti mérés alapján van mire méretezni: az API-knak 400 MB bőséges, a
többinek a mért érték kétszerese. Mellékhaszon: a .NET GC beolvassa a cgroup limitet, és
ahhoz méretezi a heapet.

**3. Hosszabb távon: ne a hoszton épüljenek a képek.** Ha a build CI-ban történne és a
szerver csak `pull`-ozna, a legnagyobb memóriacsúcs eltűnne a gépről. Ez viszont már a
telepítési folyamat átalakítása, nem compose-változás.

### A swap bevezetése

Szerveroldali művelet, a repóban nincs nyoma - ezért áll itt. `ext4` fájlrendszeren:

```bash
sudo fallocate -l 2G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
sudo findmnt --verify --fstab        # `0 parse errors, 0 errors` kell
```

A `findmnt` egy figyelmeztetést ad (`non-bind mount source /swapfile is a directory or regular
file`), ami swap bejegyzésnél **téves riasztás** - a lényeg a `0 errors`. Ha nem `ext4`, a
`fallocate` nem jó: a swap fájl nem lehet sparse/CoW, ott `dd` kell (btrfs-en `chattr +C` is).

**`vm.swappiness=10`, és ez nem opcionális:**

```bash
echo 'vm.swappiness=10' | sudo tee /etc/sysctl.d/99-swap.conf
sudo sysctl --system
```

Az alapérték 60, ami akkor is lapoz, amikor nincs rá szükség. Itt a swap **tartalék egy
csúcsra**, nem rendszeres lapozás: 60-as értéken a PostgreSQL és a .NET heap lapjai üzem közben
is kikerülhetnének a diszkre, és a szolgáltatás lassabb lenne, miközben van szabad memória. A
beállítás nélkül a swap hozzáadása rontana is valamit, nem csak javítana.

> **Újraindítás előtt nyisd meg a VPS konzolt** egy másik fülön. Az `/etc/fstab` módosítása az
> a változtatás, ami után egy gép nem jön vissza - és akkor SSH-n már nem tudsz bejutni, csak
> a konzolról vagy rescue rendszerből.

### Az első mérés: a swap dolgozott

Deploy közben, 2026-11-09 (`free -h` ismételve). A döntő összefüggés nem az, hogy a swap
megtelt, hanem hogy **a RAM-használat pontosan ugyanannyival csökkent**:

| | RAM `used` | Swap `used` | `available` |
|---|---|---|---|
| build előtt | 2.5 GiB | 76 KiB | 1.3 GiB |
| build közben | 2.4 → 2.3 GiB | 108 MiB | 1.3 → 1.4 GiB |
| **csúcs** | 2.1 GiB | **809 MiB** | 1.6 GiB |
| build vége | **1.7 GiB** | 664 MiB | **2.0 GiB** |
| deploy után | 1.9 GiB | 655 MiB | 1.8 GiB |

A RAM 2.5 → 1.7 GiB-re esett (820 MiB), a swap ugyanekkor 0-ról 809 MiB-ra nőtt: gyakorlatilag
**1:1 átköltöztetés**, nem véletlen egybeesés. És az `available` a build alatt **nőtt**, 1.3 →
2.0 GiB - pontosan akkor keletkezett tartalék, amikor kellett. Swap nélkül ennek a 820 MiB-nak
nem lett volna hova mennie.

Megfigyelés a felületről: a Dokploy admin felülete korábban **lefagyott** deploy közben, most
nem. Ez közvetett, de összhangban van a fentivel.

**A 655 MiB a deploy után nem szivárgás.** A Linux nem tölti vissza buzgón a kiírt lapokat: ami
kikerült, ott marad, amíg valaki hozzá nem nyúl. Ezek hideg lapok - jó eséllyel a Dokploy
Node-heapje és a tétlen platform-szolgáltatások.

**Egy fenntartás, amit ki kell mondani.** Ez a deploy **közvetlenül egy gépújraindítás után**
futott (hét felgyűlt kernelfrissítés miatt), tehát hideg gyorsítótár és frissen indult,
kis heapű konténerek mellett. A „nagyon gyors volt" ebből is jön, nem csak a swapból. Amit a
mérés **igazol**: a terhelés ugyanúgy bekövetkezett (809 MiB), és a swapra ment, nem a RAM-ot
szorította. Amit **nem** izolál: mennyit ért ebből az újraindítás. Ehhez egy második,
bemelegedett állapotban végzett deploy kell - lásd lentebb.

### Mit érdemes figyelni üzem közben

```bash
free -h                 # az AVAILABLE oszlopot olvasd, ne a free-t
swapon --show           # a USED oszlop
```

A `free` oszlop szándékosan alacsony: a Linux a szabad memóriát lapgyorsítótárnak használja. Az
`available` mondja meg, mennyit tudna a kernel azonnal odaadni.

| Jel | Mit jelent |
|---|---|
| `available` ~1.2-1.4 GiB körül megáll | normál plató, ez a várt állapot |
| `available` tartósan 400 MiB alatt | valódi szűkösség - itt jön szóba a 2. intézkedés |
| swap `USED` > 0, de nem nő | volt egy csúcs, a swap elkapta: ez a kívánt működés |
| `vmstat` `si`/`so` **folyamatosan** mozog | káros lapozás, nem csúcskezelés |

Az utolsó a lényegi különbség. A swap akkor jó, ha ritkán és rövid ideig használódik; ha
állandóan lapoz, az lassulás, és akkor a limit vagy a build leköltöztetése a válasz.

**A konténerek fogyasztása, memória szerint rendezve** (a `MemUsage` szöveges, ezért a
mértékegységet át kell számolni):

```bash
docker stats --no-stream --format '{{.Name}};{{.MemUsage}};{{.CPUPerc}}' \
 | awk -F';' '{split($2,a," "); v=a[1]; u=v;
     gsub(/[0-9.]/,"",u); gsub(/[^0-9.]/,"",v);
     if(u=="GiB") v*=1024; else if(u=="KiB") v/=1024;
     printf "%8.1f MiB  %-24s %8s\n", v, $1, $3 }' \
 | sort -rn
```

A `MEM %` oszlop félrevezető, amíg nincs konténer-memórialimit: a Docker a **hoszt** teljes
memóriáját veszi 100%-nak, ezért minden érték alacsonynak látszik. Addig a `MemUsage` absztolút
száma a használható.

### Deploy utáni ellenőrzés: megölt-e valamit a csúcs?

A 7. szakasz eredeti kockázata az volt, hogy **egy telepítés kinyír egy futó szolgáltatást**.
Ez az a három ellenőrzés, ami erre választ ad - deploy után, amíg friss:

```bash
# 1. Lőtt-e az OOM killer ebben a bootciklusban?
sudo dmesg -T | grep -iE 'killed process|out of memory'
journalctl -k -b --no-pager | grep -iE 'oom|killed process'

# 2. A döntő, konténerszintű válasz
for c in $(docker ps -aq); do
  docker inspect -f '{{.Name}}  OOMKilled={{.State.OOMKilled}}  Restarts={{.RestartCount}}' "$c"
done

# 3. Fent van-e minden
docker ps --format '{{.Names}}\t{{.Status}}'
```

Ha az 1. és a 2. üres, illetve mindenhol `OOMKilled=false`, akkor az a deploy nem ölt meg
semmit.

### A következő deploy mérése

A `free` pillanatképek nem mutatják a lapozás **ütemét**. Egy trace sokkal többet mond:

```bash
vmstat 1 > /tmp/deploy-vmstat.log 2>&1 &
echo $! > /tmp/vmstat.pid
#  ... deploy ...
kill "$(cat /tmp/vmstat.pid)"

# csak azok a sorok, ahol tényleg lapozott (si / so):
awk 'NR>2 && ($7>0 || $8>0)' /tmp/deploy-vmstat.log
```

A jó minta: rövid löket a build alatt, utána nulla.

### Ami a mérésből hátravan

Két dolog, és **ezek nélkül a fenti táblázatot nem érdemes átírni**:

**1. Bemelegedett újramérés.** Az újraindítás utáni első `docker stats` szerint az alkalmazás
csoportja ~953 MiB (a 10-30-i ~829 helyett), a platform ~1062 MiB (a ~1117 helyett). A két API
*kevesebb* (161 + 134 MiB a 204 + 169 helyett), ahogy hidegen várható - viszont a
**`pm-file-storage` 259.6 MiB hidegen**, miközben a 10-30-i bontásból visszaszámolva a
postgres + SeaweedFS + Redis + frontend **együtt** ~152 MiB volt. A SeaweedFS tehát érdemben
többet fogyaszt, mint a legutóbbi mérésnél, és ez önmagában magyarázatot érdemel. Egy nap üzem
után újramérni.

**2. Egy második deploy, bemelegedett állapotban.** Ez izolálja a swap hatását az újraindítás
hatásától: ha akkor is gyors marad és a swap megint mozdul, a swap az ok. Addig a fenti mérés
fenntartással olvasandó.

### A healthcheckek memóriaköltsége - amit a mérés tanított

A Dokploy 0.27.1-ben a saját healthcheckje **megduplázta** a konténer memóriahasználatát: a
megvalósítás 10 másodpercenként új `curl` folyamatot indított, és minden futásnál adatbázist
kérdezett (Dokploy issue #3755). A karbantartó javaslata két pontból áll: 30-60 másodperces
intervallum, és olyan végpont, amely nem nyúl adatbázishoz.

Ezt a tanulságot a `pm-api` healthcheckje átveszi: a `/health` eleve nem kérdez adatbázist, az
intervallum pedig **30 másodperc**. Funkcionális ára nincs, mert a kiszolgálást nem ez
kapuzza, hanem a Traefik oldali health check - az a Traefik saját folyamatából kér, tehát
folyamatot nem indít, és 10 másodpercen maradhat.

---

## 8. A commit pótlás üzemeltetése

A git szolgáltatók nem küldik el minden push minden commitját: a GitLab 20, a GitHub 2048
commitnál vágja le a webhook payload commitlistáját, és a kimaradt commitok **csak a
szolgáltató API-jából** kérhetők le. A rendszer ezt a hiányt észleli, egy `CommitBackfills`
sorban feljegyzi, és a pótlást a felületről (git nézet) lehet elindítani — integrációnként
bekapcsolható egyetlen automatikus kísérlettel.

Üzemeltetési szempontból ennek a funkciónak **egy** komoly veszélye van, és nem az, hogy nem
működik: az, hogy **némán nem működik**. Egy kézi indítás kudarcát a felhasználó látja a
felületen, egy automatikus kísérlet kudarcát viszont senki — ezért kell rá rendszeres
ellenőrzés.

### Az állapotok jelentése

| Állapot | Mit jelent | Teendő |
|---|---|---|
| `Pending` | a hiány fel van jegyezve, pótlás még nem futott | semmi; a felhasználó elindíthatja |
| `InProgress` | épp fut egy lekérés | semmi, amíg friss. A foglalás **10 perc** után elévül, onnantól a sor újraindítható |
| `Completed` | a kimaradt commitok behúzva | semmi. A sor szándékosan **nem törlődik**: ez a nyoma annak, hogy a push hiányos volt |
| `Partial` | a lekérés sem adta meg a teljes intervallumot | a hiány csökkent, de nem szűnt meg; újraindítható |
| `Failed` | a lekérés hibára futott, az ok a `LastError` mezőben | lásd alább: a hibafajta dönt |

### A rendszeres ellenőrzés

Két lekérdezés elég. Az első a megmaradt hibákat kéri, a második a beragadt futásokat:

```sql
-- Sikertelen pótlások, a legutóbbival kezdve
SELECT b."CreatedAt", b."Ref", b."ReportedCommitCount", b."ReceivedCommitCount",
       b."AttemptCount", b."LastError", i."Provider", i."RepoFullName", p."Name" AS project
FROM "CommitBackfills" b
JOIN "Integrations" i ON i."Id" = b."IntegrationId"
JOIN "Projects" p ON p."Id" = i."ProjectId"
WHERE b."State" = 'Failed'
ORDER BY b."CreatedAt" DESC;

-- Beragadt futások: a foglalás 10 percnél régebbi, és a sor mégis InProgress
SELECT b."Id", b."ClaimedAt", b."ClaimedById", i."RepoFullName"
FROM "CommitBackfills" b
JOIN "Integrations" i ON i."Id" = b."IntegrationId"
WHERE b."State" = 'InProgress'
  AND b."ClaimedAt" < NOW() - INTERVAL '10 minutes';
```

A beragadt sor **nem igényel adatbázis-műveletet**: a foglalás elévülése után a felület is
újraindíthatónak jelöli, és a felhasználó egy kattintással átveheti. Kézzel állítani az
állapotot csak akkor kell, ha a sor valamiért nem jelenik meg a felületen.

A `ClaimedById` üres értéke **nem hiányzó adat**: azt jelenti, hogy a kísérletet a rendszer
indította, nem ember.

### Mit kezdjünk a hibával

A `LastError` szövege a saját megfogalmazásunk — a szolgáltató nyers válasza szándékosan nem
kerül se a sorba, se a naplóba, mert tárházneveket és egyéb, naplóba nem való részleteket
hordozhat. A hibafajták közül **egy** igényel beavatkozást:

- **hitelesítési hiba (401/403)**: ez az az eset, amely **biztosan nem oldódik meg magától**.
  Amíg valaki nem állít be új tokent az integráción, minden további kísérlet ugyanide fut. A
  naplóban `Error` szinten jelenik meg;
- **nem található (404)**: a tárházat átnevezték vagy törölték, vagy a token nem látja (a
  GitHub a privát tárházak létét nem fedi fel). Az integráció `RepoFullName` mezője
  ellenőrizendő;
- **átmeneti hiba**: a szolgáltató nem elérhető, időtúllépés, vagy elfogyott a
  kérésszám-korlát. Ezen egy későbbi újrapróbálkozás segít, tehát nincs teendő.

### Amit a naplóban keresni kell

Minden kísérlet egy strukturált sort hagy, `Commit pótlás` kezdettel. Ebben benne van az
eredmény, **mi indította** (automatikus vagy felhasználó), a hibafajta, a behúzott darabszám,
az ígért és a megkapott commitszám, a kísérlet sorszáma, az eltelt idő, a provider, az ág,
valamint az integráció és a sor azonosítója.

A szintek úgy vannak beállítva, hogy a szűrés önmagában is jelentés legyen:

| Esemény | Szint |
|---|---|
| sikeres pótlás | `Information` — a pótlás definíció szerint rendellenesség: csak akkor van, ha előtte elveszett valami |
| részleges pótlás | `Warning` |
| sikertelen kísérlet | `Warning`, a harmadik kísérlettől `Error` |
| hitelesítési hiba | `Error`, az első alkalommal is |

Ehhez jön a hiány észlelése, amely még a webhook kérésében keletkezik: `Hiányos push payload`
kezdetű `Warning`.

### Két működési részlet, ami kérdést szül

**Az automatikus kísérlet várólistája folyamaton belüli.** A webhook nem a saját kérésében kéri
le a commitokat — a szolgáltató ugyanis a lassan válaszoló webhookot hibásnak jelöli, és végül
kikapcsolja —, hanem egy háttérfutásra bízza. Ennek két következménye van: a pótlást **az a
példány** futtatja, amelyik a hiányt észlelte, és egy **újraindítás (deploy) a még le nem
futott kísérleteket elveszti**. Ilyenkor a sor `Pending` állapotban marad, tehát semmi nem
veszett el, de kézi indításra vár. Ha valaki azt jelzi, hogy „az automatikus pótlás néha nem
fut", ez a magyarázat első helyen.

**A pótlás kérésszám-korlátos**, projektenként (`git-backfill:{projectId}` kulcs, 10 futás / 10
perc). Ez az egyetlen pont, ahol a rendszer kifelé hív, és egy türelmetlen kattintássorozat azt
a szolgáltatói kvótát égetné el, amely a következő valódi pótláshoz kell. A korlátba futó
automatikus kísérlet **nem** állítja hibásra a sort: `Pending` marad, és kézzel indítható.

### Amit ez nem old meg

A szolgáltatói korlátok közül kettő a fogadó oldalról **elvileg sem észlelhető**: a GitHub a 25
MB feletti payloadot nem kézbesíti, a GitLab pedig alapértelmezés szerint nem indít webhookot,
ha egyetlen push háromnál több ágat érint (`push_event_hooks_limit`). Ezekben az esetekben a
webhook **meg sem érkezik**, tehát nincs mit észlelni, és nincs mihez pótlást rendelni.

Üzemeltetési oldalon az egyetlen hely, ahol ez utólag látszik, a **szolgáltató saját
kézbesítési naplója** (GitHub: Settings → Webhooks → Recent Deliveries; GitLab: Webhook →
Recent events). Ha egy fejlesztő azt jelzi, hogy egy pushról semmi nem jelent meg, ezt érdemes
először megnézni — ilyenkor nem a mi oldalunkon van hiba.
