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