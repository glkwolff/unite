import type { Cargo, Equipe, Nivel } from '../api/client'

const ORDEM: Nivel[] = ['Diretor', 'Gerente', 'Supervisor', 'Funcionario']

function temNivel(nivel: Nivel | null | undefined, minimo: Nivel) {
  return nivel != null && ORDEM.indexOf(nivel) <= ORDEM.indexOf(minimo)
}

function ateNivel(nivel: Nivel, teto: Nivel) {
  return ORDEM.indexOf(nivel) >= ORDEM.indexOf(teto)
}

export function podeGerenciarCargos(nivel: Nivel | null | undefined) {
  return temNivel(nivel, 'Diretor')
}

export function podeGerenciarEquipes(nivel: Nivel | null | undefined) {
  return temNivel(nivel, 'Gerente')
}

export function podeAtribuirCargo(nivel: Nivel | null | undefined) {
  return temNivel(nivel, 'Gerente')
}

export function podeGerenciarMembros(
  nivel: Nivel | null | undefined,
  equipe: Equipe,
  usuarioId: string | undefined,
) {
  return (
    podeGerenciarEquipes(nivel) ||
    (usuarioId !== undefined && equipe.supervisor?.id === usuarioId)
  )
}

export function podePublicar(nivel: Nivel | null | undefined) {
  return temNivel(nivel, 'Supervisor')
}

export function podePublicarInstitucional(nivel: Nivel | null | undefined) {
  return temNivel(nivel, 'Gerente')
}

export function cargosAtribuiveis(nivel: Nivel | null | undefined, cargos: Cargo[]) {
  if (podeGerenciarCargos(nivel)) return cargos
  if (!podeAtribuirCargo(nivel)) return []
  return cargos.filter((c) => ateNivel(c.nivel, 'Supervisor'))
}

export function podeMexerNaPessoa(
  nivel: Nivel | null | undefined,
  nivelDaPessoa: Nivel | null,
) {
  if (podeGerenciarCargos(nivel)) return true
  if (!podeAtribuirCargo(nivel)) return false
  return !temNivel(nivelDaPessoa, 'Gerente')
}
