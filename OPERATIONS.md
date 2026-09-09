# Üzemeltetési eljárások

Ez a dokumentum azokat az eljárásokat rögzíti, amelyeket az adatkezelési tájékoztató
**megígér** a felhasználóknak. Nem fejlesztői dokumentáció: akkor kell elővenni, amikor
visszaállítás, adatvédelmi incidens vagy törlési kérés történik.

A tájékoztató és a feltételek forrása: `frontend/src/routes/PrivacyPolicy.svelte` és
`Terms.svelte`. Ha itt bármi változik, azokat is **frissíteni kell**.
---

## 1. Biztonsági mentés

### Politika

| Kérdés | Érték | Hol ígértük meg |
|---|---|---|
| Mit mentünk | A teljes PostgreSQL adatbázis **és** a MinIO volume (felhasználói csatolmányok) | — |
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
funkciója végzi. A PostgreSQL automatikusan felismerhető, a MinIO pedig volume-szinten
mentődik - **ugyanabba a bucketbe**.

Figyelem: a MinIO volume a felhasználók által feltöltött fájlokat tartalmazza, vagyis a
mentés legkiszámíthatatlanabb tartalmú része. Bármi lehet benne, amit egy felhasználó egy
taskhoz csatolt.

- Ütemezés (milyen gyakran fut): `[KITÖLTENDŐ]`
- A 7 napos rotáció hol van beállítva: `[KITÖLTENDŐ]`
- Utolsó sikeres próba-visszaállítás dátuma: `[KITÖLTENDŐ]`

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

A fióktörlés strukturált naplóbejegyzést ír a Seq-be:

```
UserErasure | UserId: ... | ErasedAt: ... | Törölt refresh tokenek: ... |
Törölt jelszó-tokenek: ... | Átírt activity sorok: ...
```

Keresés a Seq-ben:

```
@Message like '%UserErasure%'
```

### Ellenőrzőlista visszaállítás után

1. Az adatbázis visszaállt, az API elindult (a migrációk automatikusan lefutnak).
2. Seq-ben lekérdezni az összes `UserErasure` bejegyzést a **mentés időbélyege óta**.
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

### Seq megőrzési ideje — kritikus csatolás

**A Seq megőrzési idejének meg kell haladnia a mentések megőrzési idejét.** Ha a napló
hamarabb évül el, mint a legrégebbi visszaállítható mentés, elveszítjük annak a nyilvántartását,
kit kell újra anonimizálni — és egy visszaállítás némán feltámasztana egy törölt fiókot.

| | Érték |
|---|---|
| Mentés megőrzése | 7 nap |
| Seq megőrzése | **legalább 30 nap** (a Seq alapértelmezése is ennyi, de explicit beállítandó) |

A retenció **a Seq saját beállítása**, nem a Dokployé és nem a `docker-compose.prod.yml`-é:
a Seq webes felületén, a *Settings -> Retention* alatt állítható. Az alapértelmezés jó, de
explicitté kell tenni, mert egy alapértelmezés csendben megváltozhat egy image-frissítéssel.
Beállított érték: `[KITÖLTENDŐ]`.

### A Seq-et NE vedd bele a mentésbe

Ez elsőre ellentmondásosnak tűnik, de fontos: a mentés ma csak a PostgreSQL-t és a MinIO
volume-ot viszi, a Seq-et nem — és **ez így helyes**.

Ha a Seq volume is a mentés része lenne, egy visszaállítás a naplót is visszagörgetné
ugyanarra az időpontra, mint az adatbázist. Pontosan azok a `UserErasure` bejegyzések vesznének
el, amelyekre a fenti ellenőrzőlista épül. A Seq értéke itt éppen az, hogy **túléli** az
adatbázis visszaállítását.

### Maradékkockázat

| Eset | Mi történik |
|---|---|
| Csak az adatbázis sérül vagy romlik el | A Seq él, a törlési napló megvan, az eljárás működik. **Ez a gyakori eset.** |
| Teljes gépvesztés | A Seq is odavész, a mentés óta történt törlésekről nincs nyilvántartás |

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

## 3. Adatvédelmi incidens

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
   milyen következménnyel. A Seq naplók ehhez a fő forrás — ne töröld őket.
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

## 4. Éles indulás előtti ellenőrzőlista

A jogi dokumentumok olyan állításokat tartalmaznak, amelyeknek az indulás pillanatában
**igaznak kell lenniük**. Ez a lista gyűjti össze, mi van még nyitva.

- [ ] **A `[KITÖLTENDŐ]` helyőrzők kitöltve** a tájékoztatóban és a feltételekben: adatkezelő
      neve, levelezési címe, kapcsolattartási e-mail cím, tárhelyszolgáltató neve és címe.
      Keresés: `grep -rn "KITÖLTENDŐ" frontend/src`
- [ ] **A NAIH elérhetőségei ellenőrizve** a naih.hu-n (emlékezetből kerültek be)
- [ ] **A Backblaze adatfeldolgozói szerződése (DPA) elfogadva.** A tájékoztató 4. pontja
      **tényként állítja**, hogy érvényben van. Néhány kattintás a Backblaze fiókban, de amíg
      nincs meg, az állítás valótlan
- [ ] **A Seq megőrzési ideje beállítva és rögzítve** (*Settings -> Retention*), meghaladva a
      mentésekét. A beállított érték beírva ebbe a dokumentumba
- [ ] **A mentés ütemezése és a 7 napos rotáció helye rögzítve** az 1. fejezetben
- [x] ~~**Az activity-leírások sablonosítása kész.**~~ Elkészült: a leírások sablont tárolnak,
      a neveket a kiolvasás helyettesíti be, így az anonimizálás magától érvényesül
- [ ] **Próba-visszaállítás elvégezve**, a 2. fejezet ellenőrzőlistájával együtt

> A dokumentumokat jogász nem nézte át. Valódi felhasználókkal induló szolgáltatásnál ez megéri.
> A szakdolgozatban ezt a fenntartást is érdemes jelezni.
