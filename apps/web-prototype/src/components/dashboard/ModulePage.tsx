import { LucideIcon } from "lucide-react";
import { ReactNode } from "react";

type Props = {
  icon: LucideIcon;
  eyebrow: string;
  title: string;
  description?: string;
  actions?: ReactNode;
  children: ReactNode;
};

export function ModulePage({ icon: Icon, eyebrow, title, description, actions, children }: Props) {
  return (
    <div className="p-6 lg:p-8 max-w-[1600px] mx-auto space-y-6">
      <header className="flex flex-col lg:flex-row lg:items-end lg:justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <div className="size-9 rounded-xl bg-primary-soft text-primary grid place-items-center">
              <Icon className="size-4.5" strokeWidth={2.2} />
            </div>
            <span className="text-[11px] font-bold uppercase tracking-[0.18em] text-primary">{eyebrow}</span>
          </div>
          <h1 className="text-3xl font-bold tracking-tight mt-3">{title}</h1>
          {description && <p className="text-sm text-muted-foreground mt-1 max-w-2xl">{description}</p>}
        </div>
        {actions && <div className="flex items-center gap-2">{actions}</div>}
      </header>
      {children}
    </div>
  );
}

export function Card({ children, className = "" }: { children: ReactNode; className?: string }) {
  return (
    <div className={`rounded-2xl border border-border bg-card shadow-soft ${className}`}>
      {children}
    </div>
  );
}

export function StatTile({ label, value, hint, tone = "default" }: {
  label: string; value: string; hint?: string;
  tone?: "default" | "primary" | "danger" | "success" | "gold";
}) {
  const tones: Record<string, string> = {
    default: "bg-card",
    primary: "bg-primary-soft/50 border-primary/15",
    danger: "bg-destructive/5 border-destructive/15",
    success: "bg-success/5 border-success/20",
    gold: "bg-gold/5 border-gold/25",
  };
  return (
    <div className={`rounded-xl border p-4 ${tones[tone]}`}>
      <div className="text-[10px] uppercase tracking-wider text-muted-foreground font-bold">{label}</div>
      <div className="text-2xl font-bold tabular-nums mt-1">{value}</div>
      {hint && <div className="text-[11px] text-muted-foreground mt-0.5">{hint}</div>}
    </div>
  );
}
