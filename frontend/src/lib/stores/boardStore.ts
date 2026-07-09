import { writable } from 'svelte/store';
import type { BoardResponse } from '../api/boardApi';
import type { ColumnResponse } from '../api/columnApi';

interface BoardState {
    boards: BoardResponse[];
    activeBoard: BoardResponse | null;
    columns: ColumnResponse[];
}

const initialState: BoardState = {
    boards: [],
    activeBoard: null,
    columns: []
};

export const boardStore = writable<BoardState>(initialState);

export function setBoards(boards: BoardResponse[]) {
    boardStore.update(state => ({ ...state, boards }));
}

export function setActiveBoard(board: BoardResponse | null) {
    boardStore.update(state => ({ ...state, activeBoard: board }));
}

export function setColumns(columns: ColumnResponse[]) {
    boardStore.update(state => ({ ...state, columns }));
}

export function clearBoard() {
    boardStore.set(initialState);
}

// SignalR handle metódusok

export function handleBoardCreated(payload: {
    id: string;
    name: string;
    description: string | null;
    isDefault: boolean;
}) {
    boardStore.update(state => ({
        ...state,
        boards: [...state.boards, payload as unknown as BoardResponse]
    }));
}

export function handleBoardUpdated(payload: {
    boardId: string;
    name: string;
    description: string | null;
    isDefault: boolean;
}) {
    boardStore.update(state => ({
        ...state,
        boards: state.boards.map(b =>
            b.id === payload.boardId
                ? { ...b, name: payload.name, description: payload.description ?? '', isDefault: payload.isDefault }
                : b
        ),
        activeBoard: state.activeBoard?.id === payload.boardId
            ? { ...state.activeBoard, name: payload.name, description: payload.description ?? '', isDefault: payload.isDefault }
            : state.activeBoard
    }));
}

export function handleBoardDeleted(payload: { boardId: string }) {
    boardStore.update(state => ({
        ...state,
        boards: state.boards.filter(b => b.id !== payload.boardId),
        activeBoard: state.activeBoard?.id === payload.boardId
            ? null
            : state.activeBoard,
        columns: state.columns.filter(c => c.boardId !== payload.boardId)
    }));
}

export function handleColumnCreated(payload: {
    id: string;
    boardId: string;
    name: string;
    position: number;
    mapsToStatus: string;
    wipLimit: number | null;
}) {
    boardStore.update(state => ({
        ...state,
        columns: [...state.columns, payload as unknown as ColumnResponse]
    }));
}

export function handleColumnUpdated(payload: {
    columnId: string;
    boardId: string;
    name: string;
    mapsToStatus: string;
    wipLimit: number | null;
}) {
    boardStore.update(state => ({
        ...state,
        columns: state.columns.map(c =>
            c.id === payload.columnId
                ? { ...c, name: payload.name, mapsToStatus: payload.mapsToStatus, wipLimit: payload.wipLimit }
                : c
        )
    }));
}

export function handleColumnDeleted(payload: { columnId: string; boardId: string }) {
    boardStore.update(state => ({
        ...state,
        columns: state.columns.filter(c => c.id !== payload.columnId)
    }));
}

export function handleColumnsReordered(payload: {
    boardId: string;
    columns: { id: string; position: number }[];
}) {
    boardStore.update(state => ({
        ...state,
        columns: state.columns.map(c => {
            const reordered = payload.columns.find(r => r.id === c.id);
            return reordered ? { ...c, position: reordered.position } : c;
        })
    }));
}