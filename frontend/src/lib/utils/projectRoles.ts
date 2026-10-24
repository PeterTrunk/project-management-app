/**
 * A projekt-szerepkörök rangsora, a backend `ProjectRoles` osztályának párja.
 *
 * Itt a rangsor **kizárólag a felület viselkedését** szabja meg: mit ajánlunk fel, és mit
 * tiltunk le. Az érdemi kikényszerítés a szerveren van (`TeamService`), mert a kliens
 * megkerülhető - egy közvetlen API-hívás nem megy át ezen a fájlon. A két hely tehát
 * szándékosan ugyanazt a szabályt mondja ki, de nem egymás helyett.
 */

/** Növekvő sorrend - az index a rang, ahogy a backend `Hierarchy` tömbjében is. */
export const ROLE_HIERARCHY = ['Viewer', 'Member', 'Admin', 'Owner'] as const;

export type ProjectRole = (typeof ROLE_HIERARCHY)[number];

/** A szerepkör rangja, vagy -1 ha ismeretlen. Fail-closed: ismeretlen érték nem kap jogot. */
export function rankOf(role: string | null | undefined): number {
    if (!role) return -1;
    return ROLE_HIERARCHY.indexOf(role as ProjectRole);
}

/**
 * Megtehet-e a hívó valamit egy adott taggal.
 *
 * A szabály: csak a nálad **alacsonyabb** rangú tagot érintheted. Ebből adódik, hogy
 * adminisztrátor nem érhet másik adminisztrátorhoz, a tulajdonoshoz senki, és saját magához
 * sem érhet senki - mindhárom külön feltétel nélkül.
 */
export function outranks(callerRole: string | null | undefined, targetRole: string | null | undefined): boolean {
    const caller = rankOf(callerRole);
    const target = rankOf(targetRole);

    //Ismeretlen szerepkör esetén a -1 miatt a feltétel nem teljesül
    return caller > target && target >= 0;
}

/**
 * Azok a szerepkörök, amelyeket a hívó kioszthat: a sajátjánál alacsonyabbak.
 *
 * A tulajdonosi szerepkör soha nem szerepel köztük, mert nem kiosztható - a projekt
 * létrehozásával keletkezik, és nem adható át.
 */
export function assignableRoles(callerRole: string | null | undefined): ProjectRole[] {
    const caller = rankOf(callerRole);

    return ROLE_HIERARCHY.filter(
        (role) => role !== 'Owner' && rankOf(role) < caller
    ) as ProjectRole[];
}
