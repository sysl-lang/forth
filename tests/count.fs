\ Loaded after tester.fr and before a test file: counts the tests as they run, so that a run can be
\ reported as N of M. tester.fr's own T{ does nothing, so this one does only the counting.

DECIMAL
VARIABLE #TESTS   0 #TESTS !
: T{   1 #TESTS +! ;
