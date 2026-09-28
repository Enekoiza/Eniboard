import { useEffect } from "react";
import type { ReactNode } from "react";
import { Link, useNavigate } from "react-router-dom";
import { NewCardModal } from "./NewCardModal";
import { useAuthStore } from "../stores/authStore";
import { useUiStore } from "../stores/uiStore";

interface AppShellProps {
  children: ReactNode;
}

export function AppShell({ children }: AppShellProps) {
  const openNewCardModal = useUiStore((state) => state.openNewCardModal);
  const logout = useAuthStore((state) => state.logout);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const navigate = useNavigate();

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      const isModifierPressed = event.metaKey || event.ctrlKey;
      if (isModifierPressed && event.key.toLowerCase() === "k") {
        event.preventDefault();
        const isDragActive = useUiStore.getState().isDragActive;
        const hasOpenDialog = document.querySelector('[aria-modal="true"]') !== null;
        if (isDragActive || hasOpenDialog) {
          return;
        }
        openNewCardModal();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [openNewCardModal]);

  const handleLogout = () => {
    logout();
    navigate("/login", { replace: true });
  };

  return (
    <div className="bg-app-gradient min-h-screen text-gray-100">
      <header className="border-b border-white/10 bg-gray-950/60 backdrop-blur-md">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
          <Link to="/" className="text-lg font-semibold text-white">
            Eniboard
          </Link>
          {isAuthenticated ? (
            <button
              type="button"
              onClick={handleLogout}
              className="rounded-lg border border-gray-700 px-3 py-1.5 text-sm text-gray-300 hover:bg-gray-800 hover:text-white"
            >
              Log out
            </button>
          ) : null}
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-6">{children}</main>

      <button
        type="button"
        onClick={() => openNewCardModal()}
        title="New card (Ctrl+K)"
        aria-label="New card"
        className="fixed bottom-6 right-6 flex h-14 w-14 items-center justify-center rounded-full bg-brand text-2xl font-semibold text-white shadow-xl transition-transform hover:scale-105 hover:bg-brand-dark"
      >
        +
      </button>

      <NewCardModal />
    </div>
  );
}
