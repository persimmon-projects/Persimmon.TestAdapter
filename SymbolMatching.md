# TODO: Symbol matching...

## Failed fixup  -> Fixed.
* failure (context)  -> Fixed.
* success (context)  -> Fixed.

## Label not followed  -> Fixed.
* test1  -> Fixed.
* test2 (global)  -> Fixed.
* test2 (MyClass)  -> Fixed.
* test3  -> Fixed.
* test4  -> Fixed.
* tests5
  * case parameterize test  -> Fixed.
* tests6
  * source parameterize test  -> Fixed.

## No pickup
* invalid: tests
  * success test(list) : list  -> Pickup but not valid range (ignore? require validation)
  * failure test(list) : list
* invalid: tests2
  * success test1(array) : array  -> Pickup but not valid range (ignore? require validation)
  * success test2(array) : array
  * failure test(array) : array
* invalid: tests3
  * failure test(seq) : seq  -> Pickup but not valid range (ignore? require validation)
* invalid tests4
  * success test(list with value)  -> Pickup but not valid range (ignore? require validation)
  * failure test(list with value)

* --> Sequence not iterated.

## Parameter expected demangle
* should output full name  -> And invalid display name. (DisplayName == SymbolName ?)
* printer should print list in a row  -> And invalid display name. (DisplayName == SymbolName ?)
