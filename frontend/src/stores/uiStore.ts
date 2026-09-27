import { create } from "zustand";

interface NewCardModalDefaults {
  appId?: string;
  columnId?: string;
}

interface UiState {
  isNewCardModalOpen: boolean;
  newCardDefaults: NewCardModalDefaults;
  openNewCardModal: (defaults?: NewCardModalDefaults) => void;
  closeNewCardModal: () => void;
}

export const useUiStore = create<UiState>((set) => ({
  isNewCardModalOpen: false,
  newCardDefaults: {},
  openNewCardModal: (defaults = {}) => set({ isNewCardModalOpen: true, newCardDefaults: defaults }),
  closeNewCardModal: () => set({ isNewCardModalOpen: false, newCardDefaults: {} }),
}));
