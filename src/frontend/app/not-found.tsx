import Link from "next/link";

export default function NotFound() {
  return (
    <section className="rounded-xl border border-line bg-white p-8 text-center">
      <h1 className="font-serif text-3xl text-ink">Página não encontrada</h1>
      <p className="mt-2 text-sm text-muted">A vaga pode ter sido removida ou o link está inválido.</p>
      <Link
        href="/"
        className="mt-5 inline-block rounded-md border border-ink px-4 py-2 text-sm font-semibold text-ink transition hover:bg-ink hover:text-white"
      >
        Voltar para vagas
      </Link>
    </section>
  );
}
