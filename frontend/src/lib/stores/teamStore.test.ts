import { describe, it, expect } from 'vitest';
import { decideMemberRemoved } from './teamStore';

const ME = 'user-1';
const MASIK = 'user-2';
const NYITOTT = 'project-a';
const HATTER = 'project-b';

describe('decideMemberRemoved', () => {
    it('másik tag eltávolítása csak a taglistát érinti', () => {
        expect(decideMemberRemoved({ userId: MASIK, projectId: NYITOTT }, ME, NYITOTT))
            .toEqual({ kind: 'other' });
    });

    it('saját eltávolításkor lemondjuk a feliratkozást és kilépünk a nézetből', () => {
        expect(decideMemberRemoved({ userId: ME, projectId: NYITOTT }, ME, NYITOTT))
            .toEqual({ kind: 'self', leaveProjectId: NYITOTT, exitView: true });
    });

    //A két döntő eset. A kliens projektváltáskor nem lép ki a korábbi csoportból, tehát
    //olyan projektről is kaphat tageseményt, amit éppen nem néz.
    it('háttérben lévő projektből kivéve a megnyitott nézet marad', () => {
        expect(decideMemberRemoved({ userId: ME, projectId: HATTER }, ME, NYITOTT))
            .toEqual({ kind: 'self', leaveProjectId: HATTER, exitView: false });
    });

    it('az eseményben megnevezett projektből lép ki, nem a megnyitottból', () => {
        const action = decideMemberRemoved({ userId: ME, projectId: HATTER }, ME, NYITOTT);

        expect(action.kind).toBe('self');
        expect(action.kind === 'self' && action.leaveProjectId).toBe(HATTER);
    });

    it('bejelentkezett felhasználó nélkül nem dönt magunkról', () => {
        expect(decideMemberRemoved({ userId: ME, projectId: NYITOTT }, undefined, NYITOTT))
            .toEqual({ kind: 'other' });
    });

    it('megnyitott projekt nélkül nem léptet ki, de a feliratkozást lemondja', () => {
        expect(decideMemberRemoved({ userId: ME, projectId: NYITOTT }, ME, undefined))
            .toEqual({ kind: 'self', leaveProjectId: NYITOTT, exitView: false });
    });
});
