# forth

A Forth written in [sysl](https://sysl.sh): a direct-threaded core of Forth 2012's CORE word set,
with cells as wide as the machine's pointer, that passes John Hayes's Core tests.

**It is a sysl stressor first.** A fast Forth asks a language for things most programs never do:
guaranteed tail calls through a table of code addresses, registers that live in parameters across an
unbounded chain of functions, a dictionary that is one flat byte region addressed by plain numbers,
double-width arithmetic, and all of it with no allocator so that it can run on a board. Wherever sysl
could not say one of those directly, the gap is written up below rather than worked around quietly.

```
$ forth
: SQUARE  DUP * ;
7 SQUARE . CR
49
: COUNTDOWN  0 SWAP DO I . -1 +LOOP ;
3 COUNTDOWN CR
3 2 1 0
```

## Running it

```
sysl build .
./forth file.fs ...      interpret each file in turn, then end
./forth < file.fs        interpret standard input to its end
```

`BYE` ends the program. A fault — an undefined word, a stack underflow or overflow, a division by
zero, a full dictionary — is reported on a line of its own, the stacks are emptied, any definition
that was being compiled is taken back out, and interpretation goes on with the next line. Everything
defined before the fault stays. Names are found whatever their case.

## The words

156 of them:

- **Stack:** `DUP ?DUP DROP SWAP OVER ROT NIP TUCK 2DUP 2DROP 2SWAP 2OVER >R R> R@ DEPTH PICK ROLL`
- **Arithmetic and logic:** `+ - * / MOD /MOD */ */MOD AND OR XOR INVERT NEGATE ABS 1+ 1- 2* 2/
  LSHIFT RSHIFT MIN MAX = <> < > U< U> 0= 0<> 0< 0> S>D M* UM* UM/MOD SM/REM FM/MOD TRUE FALSE BL`
- **Memory:** `@ ! +! C@ C! 2@ 2! HERE ALLOT , C, ALIGN ALIGNED CELLS CELL+ CHARS CHAR+ COUNT FILL
  MOVE`
- **Control flow:** `IF ELSE THEN BEGIN UNTIL AGAIN WHILE REPEAT DO ?DO LOOP +LOOP I J LEAVE UNLOOP
  EXIT RECURSE EXECUTE`
- **Defining words and the compiler:** `: ; CREATE VARIABLE CONSTANT DOES> IMMEDIATE ' ['] [ ]
  LITERAL COMPILE, POSTPONE >BODY STATE`
- **The parser and interpreter:** `WORD PARSE PARSE-NAME FIND EVALUATE SOURCE SOURCE-ID REFILL >IN
  >NUMBER BASE DECIMAL HEX ( \ CHAR [CHAR] S" ." .( ABORT"`
- **Output and input:** `EMIT TYPE CR SPACE SPACES . U. .S <# # #S #> HOLD SIGN KEY ACCEPT WORDS
  ENVIRONMENT?`
- **Leaving:** `ABORT QUIT BYE`

The text interpreter also reads `#123`, `$FF`, `%101` and `'c'`, Forth 2012's number prefixes.

**Division is symmetric**: `/`, `MOD`, `/MOD`, `*/` and `*/MOD` round the quotient toward zero, as C
does, and the remainder takes the dividend's sign. Forth 2012 leaves the choice to the
implementation; `FM/MOD` gives floored division where a program wants it. A double cell is two cells,
low cell deeper, and its arithmetic is done in 128 bits.

`ENVIRONMENT?` answers false to every query. There is no `THROW`/`CATCH`, no `DOUBLE` word set and no
`WORDLIST`s yet.

## How it is built

Two modules and a program:

| | |
|---|---|
| `sh.sysl.forth` | the machine, the inner and outer interpreters, every word, and `ScriptConsole` — a console over text in memory. **`@no_alloc` and no capability**, so it builds for `thumb-freestanding-softfp` as it stands |
| `sh.sysl.forth.host` | `HostConsole`: standard output, standard input as the keyboard, and the source either a file or standard input |
| `main.sysl` | `forth file.fs` and `forth < file.fs` |

**The core talks to the world only through `Console`**: `emit`, `send`, `key`, `accept` and
`refill`. `refill` is where the text interpreter's lines come from and `accept`/`key` are the
keyboard, so a file being run and the keyboard `ACCEPT` reads stay separate.

**The dictionary is one region the caller hands over**, fixed in size, holding headers, names,
threads and data. A header is a link cell, a flags byte, a length byte and the name, padded to a
cell; then the code cell (the execution token is its address), a `does` cell, and the body, so
`>BODY` is two cells past the token for every word. The data and return stacks are arrays in the
machine. Addresses are real machine addresses: `@` reads whatever the number names.

## The threading model, and what `NEXT` compiles to

**Direct threading through guaranteed tail calls.** A thread is a run of cells, each the address of a
primitive, and every primitive has one signature — the machine, the instruction pointer, the data
stack pointer and the top of the stack:

```sysl
type Code = *extern(*Vm, *usize, *isize, isize) -> unit

@inline
next(vm: *Vm, ip: *usize, sp: *isize, tos: isize)
    val f: Code = ptr_cast(ip[0])

    become f(vm, &ip[1], sp, tos)

p_plus(vm: *Vm, ip: *usize, sp: *isize, tos: isize)
    if sp - vm.s0 < 2 then become underflow(vm, ip, sp, tos)

    become next(vm, ip, &sp[isize(-1)], sp[0] + tos)
```

`become` is sysl's guaranteed tail call — LLVM's `musttail` — so each primitive *replaces* its frame
with the next one's. A thread of any length runs in one native frame, and the instruction pointer,
stack pointer and top of stack stay in registers the whole way. On AArch64, at the default `-O1`,
`+` is

```
ldr  x8, [x0, #0x18]     ; the stack's base
sub  x8, x2, x8
cmp  x8, #0xf            ; two items?
b.gt 1f
...                      ; the underflow path: save the registers, record -4, ret
1: ldr  x8, [x2], #-0x8  ; second item, and pop
   ldr  x4, [x1], #0x8   ; NEXT: load the next cell, step ip
   add  x3, x8, x3
   br   x4               ; NEXT: jump
```

so **`NEXT` is two instructions, `ldr x4, [x1], #8` and `br x4`**, with no call, no return and no
dispatch loop. A fault ends the chain by *returning* instead: every frame in between was replaced, so
the return lands in `run`, which started the thread.

A word that is not a primitive is compiled as two cells: the address of its code (`docol` for a colon
definition, `dovar`, `doconst`, `dodoes`, or `doslow` for a word written in sysl) and its execution
token, which that code reads as an operand. `docol` pushes the return address and jumps to the body —
on AArch64, `ldr x8, [x1]; ldr x4, [x8, #0x10]; add x1, x8, #0x18; br x4` after the return-stack
push. The parser, the compiler and output are ordinary sysl functions reached through `doslow`; none
is on a hot path.

On `thumb-freestanding-softfp` the same `+` is a tail call too, but LLVM keeps the frame pointer and
saves `r7` and `lr` around the jump, so a Cortex-M `NEXT` costs a push and a pop besides.

## Tests

`sysl test .` runs 36 tests:

- **The Hayes Core tests: 638 of 638 pass.** `tests/tester.fr` and `tests/core.fr` are John Hayes's
  files from [gerryjackson/forth2012-test-suite](https://github.com/gerryjackson/forth2012-test-suite)
  at commit `4c87e5659c5b79bd7a6664daa56603df080c6f9b`, unchanged and with their copyright notices;
  `tests/count.fs` counts the tests as they run. The sysl test embeds all three and runs them through
  a `ScriptConsole`. From a shell, the same run is
  `./forth tests/tester.fr tests/count.fs tests/core.fr tests/report.fs < /dev/null`, which prints
  `tests: 638 failed: 0` after core.fr's own output.
- The inner interpreter: a hand-built thread, a fault leaving through `run`, nested colon
  definitions, ten million loop iterations in one native frame, `EXECUTE` of every kind of word,
  `DOES>`, `LEAVE`/`UNLOOP`, `+LOOP` across zero.
- The dictionary: the header layout byte for byte, what `,` `C,` `ALLOT` and `ALIGN` lay down, search
  order, `IMMEDIATE`, case.
- The number parser: prefixes, signs, characters, refusals, `BASE`, `>NUMBER`, the cell's full range.
- Every fault: underflow at the prompt and inside a thread, an undefined word at the prompt and inside
  a definition, a dictionary full by `ALLOT` and while compiling, division by zero in each of the
  seven dividing words, return-stack overflow, data-stack overflow, `QUIT`, `ABORT`, `ABORT"`, `BYE`.
- The host console's line splitting.

## What sysl could not say

Found writing this, each with a reduction:

1. **`p[-1]` on a raw pointer is refused** — `the literal -1 does not fit usize` — while
   `p[isize(-1)]` compiles and reads the element before `p`. The reference has `&p[n]` as the only
   spelling of an offset, so a negative literal there is legal and the literal's type is the bug.
   The core writes `&sp[isize(-1)]` sixty-odd times.
2. **`return f()` where `f` returns `unit` is refused** — `cannot return a value from a function
   with no return type` — though `unit` is "an ordinary type" and `g() = f()` is accepted.
3. **A module `val` over a literal is a load from memory at every use in a multi-unit build**:
   `val E_UNDERFLOW: isize = -4` became `adrp; ldr` in every primitive's fault path, and `val CELL =
   sizeof(isize)` a load inside `docol`'s hot path, while `SYSL_CODEGEN_UNITS=1` gives `mov x8, #-4`.
   The constant sits in one codegen unit and the others see an external global. The core uses
   `const`, which folds.
4. **Nothing names "the integer twice as wide as `isize`"**, which is what a Forth double cell is,
   and `#if` chooses by processor rather than by address width. The core does double-cell arithmetic
   in `i128` on every target: exact, but twice the work a 32-bit board needs.

## What's next

- **A REPL**, through [`sysl-lang/repl`](https://github.com/sysl-lang/repl): a `Console` whose
  `refill` is the line editor, with `ok` prompts.
- **A benchmark against gforth and pForth**, for the threaded code against two mature Forths written
  in C.
- **A Pico 2 front end**: the core already builds for `thumb-freestanding-softfp` with a
  `ScriptConsole`; what is missing is a `Console` over the USB serial line and a region in SRAM.

## License

ISC — see `LICENSE`. The files under `tests/` from the Forth 2012 test suite keep their own notice:
*(C) 1995 Johns Hopkins University / Applied Physics Laboratory; may be distributed freely as long as
this copyright notice remains.*
