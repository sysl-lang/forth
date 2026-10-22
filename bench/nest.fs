\ Two nested DO loops around a little arithmetic: two hundred million passes of I J * + 7 XOR.
\ Prints the running value, which is the same on every 64-bit Forth.

: NEST ( -- n )  0  200000 0 DO  1000 0 DO  I J * + 7 XOR  LOOP  LOOP ;

NEST . CR
BYE
