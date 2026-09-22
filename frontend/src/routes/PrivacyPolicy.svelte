<script lang="ts">
    import LegalDocument from '../lib/components/LegalDocument.svelte';
    import {
        LEGAL_CONTROLLER_NAME,
        LEGAL_CONTROLLER_ADDRESS,
        LEGAL_CONTACT_EMAIL,
        LEGAL_HOSTING_PROVIDER,
        isPlaceholder
    } from '../lib/legal';
</script>

<LegalDocument
    title="Adatkezelési tájékoztató"
    intro="Ez a tájékoztató azt írja le, hogy a szolgáltatás milyen személyes adatokat kezel, milyen célból és meddig, valamint hogy Te milyen jogokkal élhetsz ezekkel az adatokkal kapcsolatban. Igyekeztünk közérthetően fogalmazni: ahol jogszabályi hivatkozás szerepel, az az általános adatvédelmi rendeletre (GDPR) utal."
>
    <h2>1. Az adatkezelő</h2>

    <p>
        Az adatkezelő neve:
        <span class:todo={isPlaceholder(LEGAL_CONTROLLER_NAME)}>{LEGAL_CONTROLLER_NAME}</span><br />
        Levelezési cím:
        <span class:todo={isPlaceholder(LEGAL_CONTROLLER_ADDRESS)}>{LEGAL_CONTROLLER_ADDRESS}</span><br />
        Kapcsolattartási e-mail cím:
        <span class:todo={isPlaceholder(LEGAL_CONTACT_EMAIL)}>{LEGAL_CONTACT_EMAIL}</span>
    </p>

    <p>
        Adatvédelmi tisztviselő kijelölésére nem került sor, mert a szolgáltatás nem végez
        nagy számú érintettre kiterjedő, rendszeres és szisztematikus megfigyelést, és nem
        kezel különleges kategóriájú adatokat.
    </p>

    <h2>2. Milyen adatokat kezelünk</h2>

    <table>
        <thead>
            <tr>
                <th>Adatkör</th>
                <th>Konkrét adatok</th>
                <th>Cél</th>
                <th>Jogalap</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td>Fiókadatok</td>
                <td>
                    E-mail cím, megjelenítendő név, a jelszó titkosított lenyomata, a
                    kétfaktoros hitelesítés titkosított kulcsa (ha bekapcsoltad), az e-mail
                    cím megerősítettségének ténye, a regisztráció és a módosítás időpontja
                </td>
                <td>A fiók létrehozása, a bejelentkezés és a fiók működtetése</td>
                <td>Szerződés teljesítése &mdash; 6. cikk (1) b)</td>
            </tr>
            <tr>
                <td>Munkamenet-adatok</td>
                <td>
                    Bejelentkezést fenntartó tokenek és lejáratuk, jelszó-visszaállító és
                    e-mail megerősítő tokenek
                </td>
                <td>Bejelentkezve maradás, jelszó-visszaállítás, e-mail cím igazolása</td>
                <td>Szerződés teljesítése &mdash; 6. cikk (1) b)</td>
            </tr>
            <tr>
                <td>Projekttartalom</td>
                <td>
                    Projektek, feladatok, sprintek, táblák, oszlopok, címkék, hozzárendelések,
                    hozzászólások és a feltöltött fájlok, valamint az, hogy melyiket ki hozta létre
                </td>
                <td>A szolgáltatás rendeltetésszerű működése</td>
                <td>Szerződés teljesítése &mdash; 6. cikk (1) b)</td>
            </tr>
            <tr>
                <td>Tevékenységnapló</td>
                <td>Ki, mikor, milyen műveletet végzett egy projektben</td>
                <td>
                    A projekt előzményeinek követhetősége a projekt tagjai számára, a változások
                    visszakereshetősége
                </td>
                <td>Jogos érdek &mdash; 6. cikk (1) f)</td>
            </tr>
            <tr>
                <td>Git integráció</td>
                <td>
                    A csatlakoztatott repository neve, a titkosítva tárolt webhook titok,
                    valamint a beérkező commitok és pull requestek szerzőjének neve és
                    e-mail címe
                </td>
                <td>A commitok és pull requestek feladatokhoz rendelése</td>
                <td>Jogos érdek &mdash; 6. cikk (1) f)</td>
            </tr>
            <tr>
                <td>Technikai naplók és visszaélés-szűrés</td>
                <td>
                    A kérések időpontja és célja, hibaesemények, a műveletet végző azonosítója,
                    valamint <strong>IP cím</strong> a kérésszám-korlátozáshoz és a
                    visszaélések felderítéséhez
                </td>
                <td>Üzemeltetés, hibakeresés, a szolgáltatás védelme a visszaélésekkel szemben</td>
                <td>Jogos érdek &mdash; 6. cikk (1) f)</td>
            </tr>
        </tbody>
    </table>

    <p>
        A jelszavadat <strong>soha nem tároljuk olvasható formában</strong>, csak annak
        egyirányú, sózott lenyomatát &mdash; ebből az eredeti jelszó nem állítható vissza.
    </p>

    <h3>Amit nem kezelünk</h3>

    <p>
        A szolgáltatás nem használ analitikai, hirdetési vagy felhasználókövető eszközt, nem
        készít profilalkotást, és nem hoz automatizált döntést. Nem kérünk és nem kezelünk
        különleges kategóriájú adatot (például egészségügyi vagy világnézeti adatot). Ha ilyet
        magad töltesz fel projekttartalomként, azért Te felelsz.
    </p>

    <h2>3. Git integráció: harmadik személyek adatai</h2>

    <p>
        Ha egy projekthez GitHub vagy GitLab repositoryt csatlakoztatnak, a szolgáltatás a
        beérkező commitokból és pull requestekből eltárolja a <strong>szerző nevét és e-mail
        címét</strong>. Ezek az adatok jellemzően olyan személyekhez tartoznak, akik maguk nem
        felhasználói a szolgáltatásnak, ezért erről a GDPR 14. cikke alapján itt adunk
        tájékoztatást.
    </p>

    <ul>
        <li>
            <strong>Az adat forrása:</strong> a csatlakoztatott repository, a repository
            kezelője által beállított webhookon keresztül. Integráció felvétele nélkül a
            szolgáltatás semmilyen adatot nem gyűjt a repositoryról.
        </li>
        <li>
            <strong>A kezelés célja:</strong> a commitok és pull requestek hozzárendelése a
            projekt feladataihoz, hogy látható legyen, melyik munkadarabhoz melyik változtatás
            tartozik.
        </li>
        <li>
            <strong>Jogalap:</strong> jogos érdek (6. cikk (1) f). A commit szerzőségének
            feltüntetése a verziókövetés rendeltetése, az adat a szerző saját közreműködése
            folytán már szerepel a repositoryban, és a feldolgozás megfelel annak, amire a
            szerző ésszerűen számíthat.
        </li>
        <li>
            <strong>Integrációt csak a projekt tulajdonosa vagy adminisztrátora vehet fel</strong>,
            és a felvételkor kifejezetten nyilatkoznia kell arról, hogy erre jogosult.
        </li>
        <li>
            <strong>Egyedi tájékoztatás:</strong> az érintett szerzők közvetlen értesítése
            aránytalan erőfeszítést igényelne, ezért a 14. cikk (5) b) pontja alapján ez a
            nyilvános tájékoztató szolgál értesítésként.
        </li>
    </ul>

    <p>
        Ha commit szerzőjeként azt szeretnéd, hogy az adataidat töröljük, írj az 1. pontban
        megadott e-mail címre. Az integráció megszüntetésével a hozzá tartozó tárolt commit- és
        pull request adatok is törlődnek.
    </p>

    <h2>4. Adatfeldolgozók</h2>

    <p>
        Az adataidat az alábbi szolgáltatók kezelik a nevünkben. A szolgáltatás adatbázisa és
        fájltárolója <strong>saját üzemeltetésű</strong>, azokhoz harmadik fél nem fér hozzá.
    </p>

    <table>
        <thead>
            <tr>
                <th>Adatfeldolgozó</th>
                <th>Tevékenység</th>
                <th>Hol</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td>
                    <span class:todo={isPlaceholder(LEGAL_HOSTING_PROVIDER)}>{LEGAL_HOSTING_PROVIDER}</span>
                </td>
                <td>A szolgáltatás és az adatbázis üzemeltetése</td>
                <td>Németország</td>
            </tr>
            <tr>
                <td>Resend</td>
                <td>
                    Rendszerüzenetek kézbesítése (e-mail cím megerősítése, jelszó-visszaállítás)
                </td>
                <td>Írország</td>
            </tr>
            <tr>
                <td>Backblaze B2</td>
                <td>Titkosított biztonsági mentések tárolása</td>
                <td>Európai Unió</td>
            </tr>
        </tbody>
    </table>

    <p>
        Minden felsorolt szolgáltató az <strong>Európai Gazdasági Térségen belül</strong> tárolja
        az adatokat, ezért harmadik országba történő adattovábbításra nem kerül sor. A biztonsági
        mentések <strong>titkosítva tárolódnak</strong>, a tárhelyszolgáltató által kezelt
        kulcsokkal, a kapcsolat pedig minden esetben titkosított.
    </p>

    <p>
        A mentéseket tároló szolgáltató anyavállalata az Egyesült Államokban működik. Az adat
        maga az Európai Unióban marad, és a szolgáltatóval adatfeldolgozói szerződés van
        érvényben, amely a szükséges garanciákat tartalmazza.
    </p>

    <h2>5. Meddig őrizzük az adatokat</h2>

    <table>
        <thead>
            <tr>
                <th>Adatkör</th>
                <th>Megőrzési idő</th>
            </tr>
        </thead>
        <tbody>
            <tr>
                <td>Fiókadatok</td>
                <td>A fiók törléséig</td>
            </tr>
            <tr>
                <td>Projekttartalom</td>
                <td>Az adott projekt törléséig</td>
            </tr>
            <tr>
                <td>Tevékenységnapló</td>
                <td>Az adott projekt fennállásáig</td>
            </tr>
            <tr>
                <td>Bejelentkezést fenntartó tokenek</td>
                <td>A lejáratot követő 30 nap (visszaélés-vizsgálat céljából)</td>
            </tr>
            <tr>
                <td>Jelszó-visszaállító tokenek</td>
                <td>Felhasználás vagy lejárat után azonnal</td>
            </tr>
            <tr>
                <td>Technikai naplók (IP címmel)</td>
                <td>30 nap</td>
            </tr>
            <tr>
                <td>Biztonsági mentések</td>
                <td>Legfeljebb 7 nap, ezt követően felülíródnak</td>
            </tr>
        </tbody>
    </table>

    <p>
        A fiók törlése esetén az adataidat <strong>véglegesen és visszafordíthatatlanul
        anonimizáljuk</strong>: az e-mail címed, a neved, a jelszavad lenyomata és a
        kétfaktoros hitelesítés kulcsa megsemmisül. A projektekben végzett műveleteid nyoma
        megmarad, de azok többé nem köthetők Hozzád &mdash; a nevedet mindenhol a
        &bdquo;Törölt felhasználó&rdquo; megjelölés váltja fel. Erre azért van szükség, mert a
        projekt előzményei a projekt többi tagjának is a munkájához tartoznak, azokat nem
        törölhetjük.
    </p>

    <p>
        A biztonsági mentések a fenti 7 napos ciklus szerint évülnek el. A mentéseket kizárólag
        üzemzavar utáni helyreállításra használjuk, és a helyreállítási eljárásunk gondoskodik
        arról, hogy a közben törölt fiókok adatai ne kerüljenek vissza.
    </p>

    <h2>6. Sütik és a böngészőben tárolt adatok</h2>

    <p>
        A szolgáltatás <strong>nem használ analitikai, hirdetési vagy harmadik féltől származó
        követő sütit</strong>, ezért süti-hozzájárulást sem kérünk. Kizárólag a működéshez
        szükséges tárolást alkalmazzuk:
    </p>

    <ul>
        <li>
            <strong>Bejelentkezést fenntartó süti:</strong> a bejelentkezés megújítására szolgál.
            <code>HttpOnly</code> beállítású (a böngészőben futó szkriptek nem olvashatják), és
            csak a hitelesítési végpontokra küldi el a böngésző.
        </li>
        <li>
            <strong>Helyi tárolás (localStorage):</strong> a választott világos vagy sötét téma,
            az általad bezárt figyelmeztető sávok állapota, valamint bejelentkezés előtt átmenetileg
            a megnyitott meghívó azonosítója. Ezek az adatok a böngésződben maradnak, hozzánk nem
            kerülnek el.
        </li>
    </ul>

    <h2>7. Adatbiztonság</h2>

    <p>
        A GDPR 32. cikke alapján az alábbi intézkedéseket alkalmazzuk:
    </p>

    <ul>
        <li>a jelszavak egyirányú, sózott lenyomatként tárolódnak (bcrypt);</li>
        <li>
            a kétfaktoros hitelesítés kulcsai és a git integrációk webhook titkai
            hitelesített titkosítással (AES-GCM) titkosítva kerülnek az adatbázisba;
        </li>
        <li>választható kétfaktoros hitelesítés;</li>
        <li>
            kérésszám-korlátozás a bejelentkezési, regisztrációs és jelszó-visszaállítási
            végpontokon;
        </li>
        <li>titkosított kapcsolat (HTTPS), kikényszerített biztonságos átvitellel;</li>
        <li>
            a hibaüzenetek nem tartalmaznak belső technikai részletet, így az adatbázis és az
            infrastruktúra felépítése nem szivárog ki;
        </li>
        <li>projektszintű jogosultságkezelés: adataidhoz csak az adott projekt tagjai férnek hozzá;</li>
        <li>titkosított biztonsági mentések.</li>
    </ul>

    <p>
        Ha az adataid biztonságát érintő incidens történik, a jogszabályi kötelezettségünknek
        megfelelően 72 órán belül bejelentjük a felügyeleti hatóságnak, és &mdash; ha az
        incidens valószínűsíthetően magas kockázattal jár Rád nézve &mdash; Téged is értesítünk.
    </p>

    <h2>8. A Te jogaid</h2>

    <p>Az adataiddal kapcsolatban az alábbi jogok illetnek meg:</p>

    <ul>
        <li><strong>Hozzáférés:</strong> tájékoztatást kérhetsz arról, milyen adatokat kezelünk Rólad.</li>
        <li>
            <strong>Helyesbítés:</strong> a pontatlan adatok javítását kérheted. A neved és az
            e-mail címed nagy részét a profilodban magad is módosíthatod.
        </li>
        <li>
            <strong>Törlés:</strong> kérheted az adataid törlését. Ezt a fiókod törlésével
            közvetlenül is elvégezheted.
        </li>
        <li><strong>Az adatkezelés korlátozása:</strong> bizonyos esetekben kérheted a kezelés felfüggesztését.</li>
        <li>
            <strong>Tiltakozás:</strong> tiltakozhatsz a jogos érdeken alapuló adatkezelés ellen
            &mdash; ilyen a tevékenységnapló, a technikai naplózás és a git integráció.
        </li>
        <li>
            <strong>Adathordozhatóság:</strong> kérheted, hogy az általad megadott adatokat
            géppel olvasható formátumban bocsássuk rendelkezésedre.
        </li>
    </ul>

    <p>
        A kéréseidet az 1. pontban megadott e-mail címre küldheted. A megkeresésekre
        <strong>legfeljebb 30 napon belül</strong> válaszolunk. A kérés teljesítése díjmentes.
    </p>

    <h2>9. Jogorvoslat</h2>

    <p>
        Ha úgy érzed, hogy az adatkezelésünk jogsértő, elsőként kérjük, keress meg minket
        közvetlenül &mdash; a legtöbb kérdés így rendezhető a leggyorsabban.
    </p>

    <p>
        Panasszal a felügyeleti hatósághoz is fordulhatsz:<br />
        <strong>Nemzeti Adatvédelmi és Információszabadság Hatóság (NAIH)</strong><br />
        Cím: 1055 Budapest, Falk Miksa utca 9&ndash;11.<br />
        Postacím: 1363 Budapest, Pf.: 9.<br />
        E-mail: ugyfelszolgalat@naih.hu &middot; Web:
        <a href="https://naih.hu" target="_blank" rel="noopener noreferrer">naih.hu</a>
    </p>

    <p>Jogaid érvényesítése érdekében bírósághoz is fordulhatsz.</p>

    <h2>10. A tájékoztató módosítása</h2>

    <p>
        A tájékoztatót időről időre módosíthatjuk. Minden változat saját verziószámot kap,
        amelyet az oldal tetején találsz. Lényeges változás esetén a szolgáltatásban is
        felhívjuk rá a figyelmedet.
    </p>
</LegalDocument>
