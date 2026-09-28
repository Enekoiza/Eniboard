import { create } from "zustand";

interface NewCardModalDefaults {
  appId?: string;
  columnId?: string;
}

interface UiState {
  isNewCardModalOpen: boolean;
  newCardDefaults: NewCardModalDefaults;
  isDragActive: boolean;
  openNewCardModal: (defaults?: NewCardModalDefaults) => void;
  closeNewCardModal: () => void;
  setDragActive: (active: boolean) => void;
}

export const useUiStore = create<UiState>((set) => ({
  isNewCardModalOpen: false,
  newCardDefaults: {},
  isDragActive: false,
  openNewCardModal: (defaults = {}) => set({ isNewCardModalOpen: true, newCardDefaults: defaults }),
  closeNewCardModal: () => set({ isNewCardModalOpen: false, newCardDefaults: {} }),
  setDragActive: (active) => set({ isDragActive: active }),
}));
