import { describe, it, expect } from 'vitest';
import { rankOf, outranks, assignableRoles, ROLE_HIERARCHY } from './projectRoles';

/**
 * A kliensoldali rangsor tesztjei.
 *
 * Ez a modul **csak a felület viselkedését** szabja meg: mit ajánl fel és mit tilt le. Az
 * érdemi kikényszerítés a szerveren van, mert a kliens megkerülhető. A tesztek mégis
 * fontosak, mert ha a felület TÖBBET ajánl fel, mint amit a szerver elfogad, a felhasználó
 * hibába fut egy olyan műveletnél, amit a felület engedett neki.
 *
 * A szerveroldali párja: `MemberRoleRankRulesTests`.
 */

describe('rankOf', () => {
    it('a hierarchia szerinti indexet adja', () => {
        expect(rankOf('Viewer')).toBe(0);
        expect(rankOf('Member')).toBe(1);
        expect(rankOf('Admin')).toBe(2);
        expect(rankOf('Owner')).toBe(3);
    });

    it('-1-et ad ismeretlen, üres és hiányzó szerepkörre', () => {
        //Fail-closed: ismeretlen érték nem kap jogot. Ha 0-t adna, a legalacsonyabb
        //szintet kapná meg, és egy elgépelt szerepkör jogosultságot jelentene.
        expect(rankOf('Adminisztrator')).toBe(-1);
        expect(rankOf('')).toBe(-1);
        expect(rankOf(null)).toBe(-1);
        expect(rankOf(undefined)).toBe(-1);
    });

    it('kis- és nagybetűérzékeny', () => {
        //A szerver is pontos egyezést vár, tehát itt sem szabad engedékenyebbnek lenni
        expect(rankOf('admin')).toBe(-1);
        expect(rankOf('OWNER')).toBe(-1);
    });
});

describe('outranks', () => {
    it('a tulajdonos minden más szerepkört felülmúl', () => {
        expect(outranks('Owner', 'Admin')).toBe(true);
        expect(outranks('Owner', 'Member')).toBe(true);
        expect(outranks('Owner', 'Viewer')).toBe(true);
    });

    it('az adminisztrátor a tagot és a megtekintőt múlja felül', () => {
        expect(outranks('Admin', 'Member')).toBe(true);
        expect(outranks('Admin', 'Viewer')).toBe(true);
    });

    it('az adminisztrátor NEM múlja felül a másik adminisztrátort', () => {
        expect(outranks('Admin', 'Admin')).toBe(false);
    });

    it('az adminisztrátor NEM múlja felül a tulajdonost', () => {
        expect(outranks('Admin', 'Owner')).toBe(false);
    });

    it('senki nem múlja felül önmagát', () => {
        //Ebből adódik, hogy a saját szerepkör nem módosítható - külön feltétel nélkül
        for (const role of ROLE_HIERARCHY) {
            expect(outranks(role, role)).toBe(false);
        }
    });

    it('a tag és a megtekintő senkit nem múl felül', () => {
        expect(outranks('Member', 'Viewer')).toBe(true);
        expect(outranks('Member', 'Member')).toBe(false);
        expect(outranks('Viewer', 'Viewer')).toBe(false);
        expect(outranks('Viewer', 'Member')).toBe(false);
    });

    it('ismeretlen szerepkör egyik oldalon sem ad jogot', () => {
        expect(outranks('Ismeretlen', 'Viewer')).toBe(false);
        expect(outranks('Owner', 'Ismeretlen')).toBe(false);
        expect(outranks(null, 'Viewer')).toBe(false);
        expect(outranks('Owner', null)).toBe(false);
    });
});

describe('assignableRoles', () => {
    it('a tulajdonos adminisztrátort, tagot és megtekintőt adhat', () => {
        expect(assignableRoles('Owner')).toEqual(['Viewer', 'Member', 'Admin']);
    });

    it('az adminisztrátor csak tagot és megtekintőt adhat', () => {
        //Ez a lényegi állítás: az "Admin" NEM szerepelhet a listában, különben a felület
        //olyan műveletet ajánlana fel, amit a szerver elutasít
        expect(assignableRoles('Admin')).toEqual(['Viewer', 'Member']);
        expect(assignableRoles('Admin')).not.toContain('Admin');
    });

    it('a tulajdonosi szerepkör SOHA nem kiosztható', () => {
        //A projekt létrehozásával keletkezik, és nem adható át
        for (const role of ROLE_HIERARCHY) {
            expect(assignableRoles(role)).not.toContain('Owner');
        }
    });

    it('a tag és a megtekintő semmit nem adhat', () => {
        expect(assignableRoles('Member')).toEqual(['Viewer']);
        expect(assignableRoles('Viewer')).toEqual([]);
    });

    it('ismeretlen szerepkör semmit nem adhat', () => {
        expect(assignableRoles('Ismeretlen')).toEqual([]);
        expect(assignableRoles(null)).toEqual([]);
    });

    it('amit felajánl, azt a felülmúlási szabály is engedi', () => {
        //A két függvény nem mondhat ellent egymásnak: amit kiosztható szerepkörként
        //felajánlunk, annak a rangja a hívó alatt kell lennie
        for (const caller of ROLE_HIERARCHY) {
            for (const granted of assignableRoles(caller)) {
                expect(rankOf(granted)).toBeLessThan(rankOf(caller));
            }
        }
    });
});
