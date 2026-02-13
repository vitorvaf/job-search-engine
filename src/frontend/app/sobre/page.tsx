export default function SobrePage() {
  return (
    <article className="max-w-2xl space-y-5">
      <h1 className="font-serif text-4xl text-ink md:text-5xl">Sobre</h1>
      <p className="text-base leading-relaxed text-muted">
        Este projeto reúne vagas já capturadas pelo backend e apresenta tudo com leitura limpa, filtros objetivos e navegação direta.
      </p>
      <p className="text-base leading-relaxed text-muted">
        As fontes e metadados são servidos pela API existente e o frontend usa um BFF interno no Next.js para manter a integração segura.
      </p>
      <a
        href="https://github.com/seu-usuario/seu-repositorio"
        target="_blank"
        rel="noreferrer"
        className="inline-block text-sm font-semibold text-ink underline decoration-line underline-offset-4"
      >
        Repositório (atualize este link)
      </a>
    </article>
  );
}
