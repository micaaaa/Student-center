import { useEffect, useRef } from 'react';
import type { ReactNode } from 'react';

export function ConfirmationDialog({
    title,
    children,
    onConfirm,
    onClose,
    busy = false,
}: {
    title: string;
    children: ReactNode;
    onConfirm: () => void;
    onClose: () => void;
    busy?: boolean;
}) {
    const dialog = useRef<HTMLDialogElement>(null);
    useEffect(() => {
        const element = dialog.current!;
        element.showModal();
        return () => element.close();
    }, []);
    return (
        <dialog
            ref={dialog}
            className="confirmation-dialog"
            aria-labelledby="confirmation-title"
            onCancel={(event) => {
                event.preventDefault();
                if (!busy) onClose();
            }}
        >
            <h2 id="confirmation-title">{title}</h2>
            {children}
            <div className="form-actions">
                <button className="secondary" disabled={busy} autoFocus onClick={onClose}>
                    Cancel
                </button>
                <button className="primary" disabled={busy} onClick={onConfirm}>
                    {busy ? 'Processing…' : 'Confirm'}
                </button>
            </div>
        </dialog>
    );
}
