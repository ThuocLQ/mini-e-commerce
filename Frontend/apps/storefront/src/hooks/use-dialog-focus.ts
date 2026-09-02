"use client";

import { RefObject, useEffect, useRef } from "react";

type UseDialogFocusInput = {
  dialogRef: RefObject<HTMLElement | null>;
  isOpen: boolean;
  onClose: () => void;
};

const focusableSelector = [
  "a[href]",
  "button:not([disabled])",
  "input:not([disabled])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

export function useDialogFocus({ dialogRef, isOpen, onClose }: UseDialogFocusInput) {
  const triggerRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!isOpen) return;

    triggerRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const focusInitialControl = window.setTimeout(() => {
      (dialogRef.current?.querySelector<HTMLElement>("[data-dialog-initial-focus]") ?? dialogRef.current?.querySelector<HTMLElement>(focusableSelector))?.focus();
    }, 0);

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
        return;
      }

      if (event.key !== "Tab" || !dialogRef.current) return;
      const controls = Array.from(dialogRef.current.querySelectorAll<HTMLElement>(focusableSelector));
      if (controls.length === 0) {
        event.preventDefault();
        dialogRef.current.focus();
        return;
      }

      const first = controls[0];
      const last = controls[controls.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    window.addEventListener("keydown", onKeyDown);
    return () => {
      window.clearTimeout(focusInitialControl);
      window.removeEventListener("keydown", onKeyDown);
      triggerRef.current?.focus();
    };
  }, [dialogRef, isOpen, onClose]);
}