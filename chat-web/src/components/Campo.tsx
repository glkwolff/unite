import type { InputHTMLAttributes } from 'react'

interface Props extends InputHTMLAttributes<HTMLInputElement> {
  rotulo: string
  ajuda?: string
}

/** Campo de formulario com rotulo, usado nas telas de login e cadastro. */
export function Campo({ rotulo, ajuda, ...resto }: Props) {
  return (
    <label className="mb-4 block">
      <span className="mb-1 block text-sm font-medium text-slate-700">{rotulo}</span>
      <input
        {...resto}
        className="w-full rounded-md border border-unite-100 px-3 py-2 outline-none focus:border-unite-400"
      />
      {ajuda && <span className="mt-1 block text-xs text-slate-400">{ajuda}</span>}
    </label>
  )
}
