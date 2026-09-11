import type { Cargo, Equipe, Nivel } from '../api/client'

/**
 * Espelho de ChatApi/Services/Permissoes.cs. Serve para nao desenhar botao que
 * o servidor vai recusar — a decisao continua sendo do backend, que refaz cada
 * uma destas checagens lendo o cargo do banco.
 *
 *                  cargos   equipes    membros           atribuir cargo
 * Diretor           CRUD     CRUD      qualquer equipe   qualquer nivel
 * Gerente           ler      CRUD      qualquer equipe   ate Supervisor
 * Supervisor        ler      ler       so a sua equipe   nao
 * Funcionario       ler      ler       nao               nao
 */

/** Mesma ordem do enum NivelHierarquico em C#: menor indice = mais poder. */
const ORDEM: Nivel[] = ['Diretor', 'Gerente', 'Supervisor', 'Funcionario']

/** Tem pelo menos o poder de `minimo`. */
function temNivel(nivel: Nivel | null | undefined, minimo: Nivel) {
  return nivel != null && ORDEM.indexOf(nivel) <= ORDEM.indexOf(minimo)
}

/** Vai no maximo ate `teto` — teto Supervisor aceita Supervisor e Funcionario. */
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

/** Gerente para cima mexe em qualquer equipe; o supervisor, so na dele. */
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

/** O gerente so distribui cargos de Supervisor para baixo. */
export function cargosAtribuiveis(nivel: Nivel | null | undefined, cargos: Cargo[]) {
  if (podeGerenciarCargos(nivel)) return cargos
  if (!podeAtribuirCargo(nivel)) return []
  return cargos.filter((c) => ateNivel(c.nivel, 'Supervisor'))
}

/** O gerente tambem nao encosta em quem ja e diretor ou gerente. */
export function podeMexerNaPessoa(
  nivel: Nivel | null | undefined,
  nivelDaPessoa: Nivel | null,
) {
  if (podeGerenciarCargos(nivel)) return true
  if (!podeAtribuirCargo(nivel)) return false
  return !temNivel(nivelDaPessoa, 'Gerente')
}
