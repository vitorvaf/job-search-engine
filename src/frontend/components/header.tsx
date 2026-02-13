import Link from "next/link";

const links = [
  { href: "/", label: "Vagas" },
  { href: "/favoritos", label: "Favoritos" },
  { href: "/sobre", label: "Sobre" },
];

export function Header() {
  return (
    <header className="border-b border-line/80 bg-canvas">
      <div className="mx-auto flex max-w-3xl items-center justify-between px-4 py-6">
        <Link href="/" className="font-serif text-xl font-semibold tracking-tight text-ink">
          Jobs.
        </Link>
        <nav aria-label="Navegação principal" className="flex items-center gap-5">
          {links.map((link) => (
            <Link key={link.href} href={link.href} className="text-sm font-medium text-muted transition hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2">
              {link.label}
            </Link>
          ))}
        </nav>
      </div>
    </header>
  );
}
