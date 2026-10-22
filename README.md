# forth

A Forth written in [sysl](https://sysl.sh): a direct-threaded core of Forth 2012's CORE word set,
with cells as wide as the machine's pointer, that passes John Hayes's Core tests.

**It is a sysl stressor first.** A fast Forth asks a language for things most programs never do:
guaranteed tail calls through a table of code addresses, registers that live in parameters across an
unbounded chain of functions, a dictionary that is one flat byte region addressed by plain numbers,
double-width arithmetic, and all of it with no allocator so that it can run on a board. Wherever sysl
could not say one of those directly, the gap goes to sysl to be fixed rather than worked around here.

```
$ forth
forth 0.1.0 -- BYE or Ctrl-D to leave
: SQUARE  DUP * ;
 ok
7 SQUARE .
49  ok
: COUNTDOWN
...   0 SWAP DO I . -1 +LOOP ;
 ok
3 COUNTDOWN
3 2 1 0  ok
```

## Usage

```
sysl build .
./forth                  at a terminal: the REPL
./forth file.fs ...      interpret each file in turn, then end
./forth < file.fs        interpret standard input to its end
```

`BYE` ends the program. A fault — an undefined word, a stack underflow or overflow, a division by
zero, a full dictionary, any `THROW` nothing catches — is reported on a line of its own with the
standard's message for its code, the stacks are emptied, any definition that was being compiled is
taken back out, and interpretation goes on with the next line. Everything defined before the fault
stays. Names are found whatever their case.

## The REPL

`forth` with no file and a terminal on standard input is a traditional Forth prompt, with the line
editor from [`sysl-lang/repl`](https://github.com/sysl-lang/repl):

- **Each line is interpreted when it is entered**, and ` ok` follows it when it ran to its end. A
  fault prints its message instead, and the next line starts fresh with the dictionary as it was.
- **A definition may span lines.** While a `:` definition is open the prompt is `... `, and each line
  is compiled as it is entered. Ctrl-C at that prompt abandons the definition; the data stack stays,
  as `QUIT` leaves it.
- **Tab completes a word from the dictionary**, built-in or your own, whatever the case you type it
  in. One candidate replaces the word; several extend it to their common start and then list.
- **The history is kept in `~/.forth_history`**, one input per line, with Up and Down to walk it. The
  editing keys are `repl`'s: Emacs-style motion and deletion, Ctrl-L to clear.
- `BYE` or Ctrl-D on an empty line leaves.

`KEY` and `ACCEPT` read the same terminal, out of raw mode while a line is being interpreted. Piped
input never starts the REPL: `forth < file.fs` reads the file as it always has, with no prompt.

## The words

187 of them:

- **Stack:** `DUP ?DUP DROP SWAP OVER ROT NIP TUCK 2DUP 2DROP 2SWAP 2OVER >R R> R@ DEPTH PICK ROLL`
- **Arithmetic and logic:** `+ - * / MOD /MOD */ */MOD AND OR XOR INVERT NEGATE ABS 1+ 1- 2* 2/
  LSHIFT RSHIFT MIN MAX = <> < > U< U> 0= 0<> 0< 0> S>D M* UM* UM/MOD SM/REM FM/MOD TRUE FALSE BL`
- **Memory:** `@ ! +! C@ C! 2@ 2! HERE ALLOT , C, ALIGN ALIGNED CELLS CELL+ CHARS CHAR+ COUNT FILL
  MOVE`
- **Control flow:** `IF ELSE THEN BEGIN UNTIL AGAIN WHILE REPEAT DO ?DO LOOP +LOOP I J LEAVE UNLOOP
  EXIT RECURSE EXECUTE`
- **Defining words and the compiler:** `: :NONAME ; CREATE VARIABLE CONSTANT DOES> IMMEDIATE ' ['] [ ]
  LITERAL COMPILE, POSTPONE >BODY STATE`
- **The parser and interpreter:** `WORD PARSE PARSE-NAME FIND EVALUATE SOURCE SOURCE-ID REFILL >IN
  >NUMBER BASE DECIMAL HEX ( \ CHAR [CHAR] S" ." .( ABORT"`
- **Output and input:** `EMIT TYPE CR SPACE SPACES . U. .S <# # #S #> HOLD SIGN KEY ACCEPT WORDS
  ENVIRONMENT?`
- **Leaving:** `ABORT QUIT BYE`
- **Exception:** `CATCH THROW`
- **Core Extension, beyond the above:** `2>R 2R> 2R@ WITHIN VALUE TO DEFER IS DEFER@ DEFER!
  ACTION-OF MARKER BUFFER: [COMPILE] CASE OF ENDOF ENDCASE C" S\" PAD ERASE UNUSED SAVE-INPUT
  RESTORE-INPUT .R U.R HOLDS`

The text interpreter also reads `#123`, `$FF`, `%101` and `'c'`, Forth 2012's number prefixes.

**Division is symmetric**: `/`, `MOD`, `/MOD`, `*/` and `*/MOD` round the quotient toward zero, as C
does, and the remainder takes the dividend's sign. Forth 2012 leaves the choice to the
implementation; `FM/MOD` gives floored division where a program wants it. A double cell is two cells,
low cell deeper, and its arithmetic is done in `DCell`, the integer twice as wide as a cell, chosen by
the target's address width with `#if ptr16` / `ptr32` / `ptr64`:

| target's addresses | cell | `DCell` |
|---|---|---|
| 64 bits (`aarch64`, `x86_64`, `riscv64`) | `isize`, 8 bytes | `i128` |
| 32 bits (`thumb`, `riscv32`, `wasm32`, `x86`) | `isize`, 4 bytes | `i64` |
| 16 bits (`craft`) | `isize`, 2 bytes | `i32` |

`ENVIRONMENT?` answers false to every query. There is no `DOUBLE` word set and no `WORDLIST`s yet.

**Every fault is a `THROW`**, with Forth 2012's code for it (-4 for a stack underflow, -13 for an
undefined word, -10 for a division by zero), so `CATCH` catches what the machine raises exactly as it
catches a program's own `THROW`. `ABORT` is `-1 THROW` and `ABORT"` is `-2 THROW`, whose message is
shown only if nothing catches it. Uncaught, a code the standard lists is reported with the standard's
message (`-9 THROW` says `invalid memory address`), an undefined word by its name and `?`, and any
other code as `uncaught exception 42`. `BYE` is the one thing `CATCH` lets through.

## How it is built

Two modules and a program:

| | |
|---|---|
| `sh.sysl.forth` | the machine, the inner and outer interpreters, every word, and `ScriptConsole` — a console over text in memory. **`@no_alloc` and no capability**, so it builds for `thumb-freestanding-softfp` as it stands |
| `sh.sysl.forth.host` | `HostConsole`: standard output, standard input as the keyboard, and the source either a file or standard input; and the REPL's evaluator, console and completion |
| `main.sysl` | `forth` at a terminal, `forth file.fs` and `forth < file.fs` |

**The core talks to the world only through `Console`**: `emit`, `send`, `key`, `accept` and
`refill`. `refill` is where the text interpreter's lines come from and `accept`/`key` are the
keyboard, so a file being run and the keyboard `ACCEPT` reads stay separate. A front end that has its
own lines, as the REPL does, hands each one to `interpret_line` instead and gets back how it ended.

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

    become next(vm, ip, &sp[-1], sp[0] + tos)
```

`become` is sysl's guaranteed tail call — LLVM's `musttail` — so each primitive *replaces* its frame
with the next one's. A thread of any length runs in one native frame, and the instruction pointer,
stack pointer and top of stack stay in registers the whole way. On AArch64, at `-O1`,
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

**That return is `THROW`, and `CATCH` is a `run`.** `THROW` records its code in the machine and
returns; `CATCH` notes the data stack's depth, the return stack pointer and the input source, and runs
its token in a nested `run`. However deep the `THROW` — colon definitions, `EXECUTE`, `EVALUATE`
inside `EVALUATE` — it returns through every sysl frame in between to that `run`, and `CATCH` puts the
three back and answers the code. No `setjmp`, no unwinding tables, and the hot path is unchanged.

A word that is not a primitive is compiled as two cells: the address of its code (`docol` for a colon
definition, `dovar`, `doconst`, `dodoes`, or `doslow` for a word written in sysl) and its execution
token, which that code reads as an operand. `docol` pushes the return address and jumps to the body —
on AArch64, `ldr x8, [x1]; ldr x4, [x8, #0x10]; add x1, x8, #0x18; br x4` after the return-stack
push. The parser, the compiler and output are ordinary sysl functions reached through `doslow`; none
is on a hot path.

On `thumb-freestanding-softfp` the same `+` is a tail call too, but LLVM keeps the frame pointer and
saves `r7` and `lr` around the jump, so a Cortex-M `NEXT` costs a push and a pop besides.

## Tests

`sysl test .` runs 68 tests:

- **The Hayes Core tests: 638 of 638 pass.** `tests/tester.fr` and `tests/core.fr` are John Hayes's
  files from [gerryjackson/forth2012-test-suite](https://github.com/gerryjackson/forth2012-test-suite)
  at commit `4c87e5659c5b79bd7a6664daa56603df080c6f9b`, unchanged and with their copyright notices;
  `tests/count.fs` counts the tests as they run. The sysl test embeds all three and runs them through
  a `ScriptConsole`. From a shell, the same run is
  `./forth tests/tester.fr tests/count.fs tests/core.fr tests/report.fs < /dev/null`, which prints
  `tests: 638 failed: 0` after core.fr's own output.
- **The Forth 2012 suite's word-set files**, from the same commit, unchanged, each run by a sysl
  test after `tester.fr` and `core.fr` (and the suite's helpers where the file needs them), with
  `count.fs` loaded just before it. Each test asserts the number of tests, zero errors in tester's
  count, and that no line faulted:

  | file | upstream path | SHA-256 | tests | errors |
  |---|---|---|---:|---:|
  | `tests/coreplustest.fth` | `src/coreplustest.fth` | `d967d1d367b3c0bf91b13944694fb5ac26fa793a63be3ea58bf50779971ef47b` | 101 | 0 |
  | `tests/coreexttest.fth` | `src/coreexttest.fth` | `f35b9a78ac463985e2cfa616f19893204e25f5a51f0512324d8d45fe9e73b71b` | 383 | 0 |
  | `tests/exceptiontest.fth` | `src/exceptiontest.fth` | `92bce976b74a799f0a7c9f4200e46832eea6371d117e522dea4550a6fe067083` | 9 | 0 |

  The suite's helpers, loaded before the word-set files as its own `runtests.fth` loads them:
  `tests/utilities.fth` (`src/utilities.fth`,
  `69b362b974f2ecebb0dbea1d77281dcca7b112107d7ad7b3e7f764a40c1c83a7`) and `tests/errorreport.fth`
  (`src/errorreport.fth`, `d348c8d5d2ad1793b118f83dda6f79d4c74c3d7abd973e8ad7d8841b8243366d`). A
  word-set file hands its error count to `errorreport.fth`'s `SET-ERROR-COUNT` at its end, so the
  test reads it from there, and checks the total over every file too.
- The inner interpreter: a hand-built thread, a fault leaving through `run`, nested colon
  definitions, ten million loop iterations in one native frame, `EXECUTE` of every kind of word,
  `DOES>`, `LEAVE`/`UNLOOP`, `+LOOP` across zero.
- The dictionary: the header layout byte for byte, what `,` `C,` `ALLOT` and `ALIGN` lay down, search
  order, `IMMEDIATE`, case.
- The number parser: prefixes, signs, characters, refusals, `BASE`, `>NUMBER`, the cell's full range.
- Every fault: underflow at the prompt and inside a thread, an undefined word at the prompt and inside
  a definition, a dictionary full by `ALLOT` and while compiling, division by zero in each of the
  seven dividing words, return-stack overflow, data-stack overflow, `QUIT`, `ABORT`, `ABORT"`, `BYE`.
- `CATCH` and `THROW`: a `THROW` from nested definitions with both stacks put back, from inside
  `EXECUTE` inside a `DO` loop that goes on after, the machine's own faults caught with their codes
  and the input source put back, `0 THROW`, the standard messages and `uncaught exception n` when
  nothing catches, `ABORT"`'s message shown only uncaught, `BYE` going through `CATCH`, and a thrown
  `1` not ending the program.
- The Core Extension words where the suite does not reach: an empty `DEFER`, `MARKER`, `CASE` with no
  match, `S\"` and `C"` interpreted, `RESTORE-INPUT` of another source, `2R>` underflow, `.R` fields,
  a negative `BUFFER:`.
- The host console's line splitting, and `interpret_line`'s answers.
- The REPL, driven through `repl`'s `MockTerminal` with scripted keys and checked against the exact
  bytes sent to the terminal: ` ok` after a line, a definition across two lines at the continuation
  prompt, an error followed by a working line, an uncaught `THROW` showing the standard message and
  the REPL going on, a caught one answered with ` ok`, an error inside a definition, Ctrl-C
  abandoning a definition, Tab completing a built-in word in lower case and a word of the user's,
  which names completion offers, `BYE`, and Ctrl-D.

## Speed

Against two mature Forths written in C: [gforth](https://gforth.org) 0.7.3 from Homebrew, both its
default engine (`gforth`) and its fastest (`gforth-fast`), and
[pForth](https://github.com/philburk/pforth) built from source at `-O2` (commit
`e5617638c3a397c644fe82c6301711867a0ca1bf`). This forth is built at `-O2`, which the manifest sets.
Seconds of wall time, the median of five runs, the systems taking turns within each round:

| benchmark | forth | gforth | gforth-fast | pForth |
|---|---:|---:|---:|---:|
| sieve | 1.052 | 1.246 | 0.787 | 2.742 |
| fib | 0.634 | 0.645 | 0.395 | 1.256 |
| nest | 0.519 | 0.799 | 0.551 | 1.162 |
| bubble | 0.817 | 0.616 | 0.345 | 1.168 |

- **sieve** — the BYTE sieve of the primes below 16384, 8000 times over an 8190-byte flag array:
  `C@`, `C!`, `DO` loops and a `BEGIN … WHILE … REPEAT`.
- **fib** — `38 FIB`, doubly recursive: calls and returns, and little else.
- **nest** — two nested `DO` loops around `I J * + 7 XOR`, two hundred million passes.
- **bubble** — a bubble sort of 1000 cells filled by a linear congruential generator, a hundred
  times: `@`, `!`, comparisons and a branch per pair.

On an Apple M5 Pro (18 cores, 64 GB), macOS 27.0.1. The sources are in `bench/` and run unchanged on
all three systems; `zsh bench/run.zsh` builds forth and prints the table, checking that every system
printed the same answer. Each time includes starting the system, which is a few milliseconds.

**forth is between gforth and gforth-fast**: ahead of the default gforth on three of the four, level
with gforth-fast on the loop kernel, and about twice as fast as pForth throughout. Every forth
primitive checks the stack depth before it touches the stack, which gforth-fast does not; where
gforth-fast's lead on fib and bubble comes from has not been measured yet.

## What's next

- **A Pico 2 front end**: the core already builds for `thumb-freestanding-softfp` with a
  `ScriptConsole`; what is missing is a `Console` over the USB serial line and a region in SRAM.

## License

ISC — see `LICENSE`. The files under `tests/` from the Forth 2012 test suite keep their own notice:
*(C) 1995 Johns Hopkins University / Applied Physics Laboratory; may be distributed freely as long as
this copyright notice remains.*
