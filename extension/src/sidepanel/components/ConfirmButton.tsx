import { useEffect, useState } from 'react';

/** Two-click delete button: first click arms it ("Sure?"), second click confirms. */
export function ConfirmButton({
  label,
  onConfirm,
  className = '',
}: {
  label: string;
  onConfirm: () => void;
  className?: string;
}) {
  const [armed, setArmed] = useState(false);

  useEffect(() => {
    if (!armed) return;
    const timer = setTimeout(() => setArmed(false), 2500);
    return () => clearTimeout(timer);
  }, [armed]);

  return (
    <button
      className={`${className} ${armed ? 'btn-danger' : ''}`.trim()}
      onClick={(e) => {
        e.stopPropagation();
        if (armed) {
          setArmed(false);
          onConfirm();
        } else {
          setArmed(true);
        }
      }}
    >
      {armed ? 'Sure?' : label}
    </button>
  );
}
