\ Bubble sort of 1000 cells filled by a linear congruential generator, a hundred times over: memory
\ traffic through @ and !, comparisons and a branch per pair. Prints the smallest and largest value.

1000 CONSTANT N
CREATE A N CELLS ALLOT
VARIABLE SEED

: RAND ( -- u )  SEED @ 1103515245 * 12345 + DUP SEED !  16 RSHIFT 32767 AND ;

: FILL-A  12345 SEED !  N 0 DO  RAND A I CELLS + !  LOOP ;

: BUBBLE
  N 1 DO
    N I - 0 DO
      A I CELLS + DUP @ OVER CELL+ @
      2DUP > IF ROT TUCK ! CELL+ ! ELSE 2DROP DROP THEN
    LOOP
  LOOP ;

: BENCH  100 0 DO FILL-A BUBBLE LOOP  A @ .  A N 1- CELLS + @ . CR ;

BENCH
BYE
