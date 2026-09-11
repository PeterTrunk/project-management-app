import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import type { BoardResponse } from '../api/boardApi';
import type { ColumnResponse } from '../api/columnApi';
import {
    boardStore,
    setBoards,
    setActiveBoard,
    setColumns,
    clearBoard,
    handleBoardCreated,
    handleBoardUpdated,
    handleBoardDeleted,
    handleColumnCreated,
    handleColumnUpdated,
    handleColumnDeleted,
    handleColumnsReordered
} from './boardStore';

function board(id: string, overrides: Partial<BoardResponse> = {}): BoardResponse {
    return {
        id,
        projectId: 'p1',
        name: `Board ${id}`,
        description: '',
        isDefault: false,
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        columns: null,
        rowVersion: 1,
        ...overrides
    } as BoardResponse;
}

function column(id: string, boardId: string, position: number): ColumnResponse {
    return {
        id,
        boardId,
        name: `Oszlop ${id}`,
        mapsToStatus: 'todo',
        wipLimit: null,
        position,
        rowVersion: 1
    } as ColumnResponse;
}

describe('boardStore', () => {
    beforeEach(() => clearBoard());

    describe('handleBoardCreated', () => {
        it('hozzáfűzi az új boardot', () => {
            setBoards([board('a')]);

            handleBoardCreated({ id: 'b', name: 'Új board', description: null, isDefault: false });

            expect(get(boardStore).boards.map(b => b.id)).toEqual(['a', 'b']);
        });
    });

    describe('handleBoardUpdated', () => {
        it('csak a megnevezett boardot módosítja', () => {
            setBoards([board('a'), board('b')]);

            handleBoardUpdated({ boardId: 'a', name: 'Átnevezve', description: null, isDefault: false });

            const state = get(boardStore);
            expect(state.boards.find(b => b.id === 'a')!.name).toBe('Átnevezve');
            expect(state.boards.find(b => b.id === 'b')!.name).toBe('Board b');
        });

        //Az aktív board külön hivatkozás: ha nem frissül, 
        //a megnyitott nézet fejléce a régi nevet mutatná, miközben a listában már az új szerepel
        it('az aktív boardot is frissíti, ha az érintett', () => {
            setBoards([board('a')]);
            setActiveBoard(board('a'));

            handleBoardUpdated({ boardId: 'a', name: 'Átnevezve', description: null, isDefault: false });

            expect(get(boardStore).activeBoard!.name).toBe('Átnevezve');
        });

        it('nem nyúl az aktív boardhoz, ha másik boardot módosítottak', () => {
            setBoards([board('a'), board('b')]);
            setActiveBoard(board('a'));

            handleBoardUpdated({ boardId: 'b', name: 'Másik', description: null, isDefault: false });

            expect(get(boardStore).activeBoard!.name).toBe('Board a');
        });

        it('a null leírást üres sztringre fordítja', () => {
            setBoards([board('a', { description: 'Eredeti' })]);

            handleBoardUpdated({ boardId: 'a', name: 'Board a', description: null, isDefault: false });

            expect(get(boardStore).boards[0].description).toBe('');
        });
    });

    describe('handleBoardDeleted', () => {
        it('kiveszi a boardot és AZ OSZLOPAIT is', () => {
            setBoards([board('a'), board('b')]);
            setColumns([column('c1', 'a', 1), column('c2', 'b', 1)]);

            handleBoardDeleted({ boardId: 'a' });

            const state = get(boardStore);
            expect(state.boards.map(b => b.id)).toEqual(['b']);
            expect(state.columns.map(c => c.id)).toEqual(['c2']);
        });

        //Törölt board után nem maradhat rá mutató aktív hivatkozás
        it('nullázza az aktív boardot, ha azt törölték', () => {
            setBoards([board('a')]);
            setActiveBoard(board('a'));

            handleBoardDeleted({ boardId: 'a' });

            expect(get(boardStore).activeBoard).toBeNull();
        });

        it('megtartja az aktív boardot, ha másikat töröltek', () => {
            setBoards([board('a'), board('b')]);
            setActiveBoard(board('a'));

            handleBoardDeleted({ boardId: 'b' });

            expect(get(boardStore).activeBoard!.id).toBe('a');
        });
    });

    describe('handleColumnCreated', () => {
        it('hozzáfűzi az oszlopot', () => {
            setColumns([column('c1', 'a', 1)]);

            handleColumnCreated({
                id: 'c2', boardId: 'a', name: 'Új oszlop',
                position: 2, mapsToStatus: 'doing', wipLimit: null
            });

            expect(get(boardStore).columns.map(c => c.id)).toEqual(['c1', 'c2']);
        });
    });

    describe('handleColumnUpdated', () => {
        it('csak a megnevezett oszlopot módosítja', () => {
            setColumns([column('c1', 'a', 1), column('c2', 'a', 2)]);

            handleColumnUpdated({
                columnId: 'c1', boardId: 'a', name: 'Átnevezve',
                mapsToStatus: 'todo', wipLimit: 3
            });

            const state = get(boardStore);
            expect(state.columns.find(c => c.id === 'c1')!.name).toBe('Átnevezve');
            expect(state.columns.find(c => c.id === 'c1')!.wipLimit).toBe(3);
            expect(state.columns.find(c => c.id === 'c2')!.name).toBe('Oszlop c2');
        });

        //A pozíciót szándékosan nem az update esemény hordozza, hanem az átrendezés
        it('nem írja felül a pozíciót', () => {
            setColumns([column('c1', 'a', 7)]);

            handleColumnUpdated({
                columnId: 'c1', boardId: 'a', name: 'Átnevezve',
                mapsToStatus: 'todo', wipLimit: null
            });

            expect(get(boardStore).columns[0].position).toBe(7);
        });
    });

    describe('handleColumnDeleted', () => {
        it('kiveszi az oszlopot', () => {
            setColumns([column('c1', 'a', 1), column('c2', 'a', 2)]);

            handleColumnDeleted({ columnId: 'c1', boardId: 'a' });

            expect(get(boardStore).columns.map(c => c.id)).toEqual(['c2']);
        });
    });

    describe('handleColumnsReordered', () => {
        //Ez a handler nem csak frissít, hanem RENDEZ is - ha a rendezés kimaradna,
        //az oszlopok a régi sorrendben maradnának a képernyőn az új pozíciók ellenére
        it('frissíti a pozíciókat és újrarendezi a listát', () => {
            setColumns([column('c1', 'a', 1), column('c2', 'a', 2), column('c3', 'a', 3)]);

            handleColumnsReordered({
                boardId: 'a',
                columns: [
                    { id: 'c1', position: 3, rowVersion: 2 },
                    { id: 'c3', position: 1, rowVersion: 2 }
                ]
            });

            expect(get(boardStore).columns.map(c => c.id)).toEqual(['c3', 'c2', 'c1']);
        });

        it('érintetlenül hagyja a fel nem sorolt oszlopokat', () => {
            setColumns([column('c1', 'a', 1), column('c2', 'a', 2)]);

            handleColumnsReordered({
                boardId: 'a',
                columns: [{ id: 'c1', position: 5, rowVersion: 9 }]
            });

            const c2 = get(boardStore).columns.find(c => c.id === 'c2')!;
            expect(c2.position).toBe(2);
            expect(c2.rowVersion).toBe(1);
        });
    });
});
